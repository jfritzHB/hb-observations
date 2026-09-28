using System.Text.Json;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FieldApp.E2ETests;

public sealed class DurableCaptureTests
{
    private const string DraftScript = """
        async () => {
          const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open('fieldapp-capture', 1);
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
          });
          const drafts = await new Promise((resolve, reject) => {
            const request = db.transaction('drafts').objectStore('drafts').getAll();
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
          });
          db.close();
          return drafts[0];
        }
        """;

    [Fact]
    public async Task Photo_preview_description_refresh_and_thumbnail_work_at_360px()
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        await PrepareCaptureAsync(phone);
        var page = phone.Page;
        await ChooseSyntheticPhotoAsync(page);
        await phone.AssertNoHorizontalScrollAsync("Photo preview");
        await phone.AssertAccessibleAsync("Photo preview");
        await page.GetByRole(AriaRole.Button, new() { Name = "Remove photo", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        await ChooseSyntheticPhotoAsync(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "Use photo", Exact = true }).ClickAsync();
        await AssertReadyAsync(phone);
        var editor = page.GetByRole(AriaRole.Textbox, new() { Name = "Description (English)" });
        await editor.FillAsync("Synthetic inspection: patch the marked drywall edge.");
        await Expect(page.GetByText("Saved on this device.", new() { Exact = false })).ToBeVisibleAsync();
        await page.ReloadAsync();
        await Expect(editor).ToHaveValueAsync("Synthetic inspection: patch the marked drywall edge.");
        await phone.AssertAccessibleAsync("Description restored");
        await phone.AssertNoHorizontalScrollAsync("Description restored");
        await PhoneSession.AssertNotObscuredAsync(editor, "English description");
        await phone.ScreenshotAsync("22-description");
        await AssertOneItemAndPhotoAsync(phone);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("upload")]
    [InlineData("finalize")]
    [InlineData("server")]
    public async Task Lost_response_refresh_and_retry_preserve_one_item_and_one_photo(string boundary)
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        await PrepareCaptureAsync(phone);
        var page = phone.Page;
        var pattern = boundary switch
        {
            "create" or "server" => "**/api/v1/projects/*/items",
            "upload" => "**/api/v1/items/*/photos/*/content",
            _ => "**/api/v1/items/*/photos/*/finalize",
        };
        var first = true;
        await page.RouteAsync(pattern, async route =>
        {
            if (boundary == "server")
            {
                await route.FulfillAsync(new() { Status = 503, ContentType = "application/problem+json", Body = "{\"title\":\"Synthetic outage\"}" });
                return;
            }

            if (first)
            {
                first = false;
                // The server commits, but the response never reaches the browser.
                await route.FetchAsync();
            }

            await route.AbortAsync();
        });
        await ChooseSyntheticPhotoAsync(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "Use photo", Exact = true }).ClickAsync();
        await Expect(page.Locator("[data-status='Failed']")).ToBeVisibleAsync();
        var before = await page.EvaluateAsync<JsonElement>(DraftScript);
        Assert.True(before.GetProperty("photo").GetProperty("byteLength").GetInt32() > 0);
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Description (English)" }).FillAsync("Keep this during retry.");
        await Expect(page.GetByText("Saved on this device.", new() { Exact = false })).ToBeVisibleAsync();
        await page.ReloadAsync();
        await Expect(page.Locator("[data-status='Failed']")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Textbox)).ToHaveValueAsync("Keep this during retry.");
        await phone.AssertAccessibleAsync("Failed capture restored");
        await page.UnrouteAsync(pattern);
        await page.GetByRole(AriaRole.Button, new() { Name = "Try again", Exact = true }).ClickAsync();
        await AssertReadyAsync(phone);
        await Expect(page.GetByRole(AriaRole.Textbox)).ToHaveValueAsync("Keep this during retry.");
        await AssertOneItemAndPhotoAsync(phone);
    }

    [Fact]
    public async Task Connection_drop_after_load_keeps_local_photo_and_reconnect_resumes()
    {
        await using var phone = await PhoneSession.StartAsync("superintendent");
        await PrepareCaptureAsync(phone);
        await ChooseSyntheticPhotoAsync(phone.Page);
        await phone.Page.Context.SetOfflineAsync(true);
        await phone.Page.GetByRole(AriaRole.Button, new() { Name = "Use photo", Exact = true }).ClickAsync();
        await Expect(phone.Page.Locator("[data-status='Failed']")).ToBeVisibleAsync();
        var local = await phone.Page.EvaluateAsync<JsonElement>(DraftScript);
        Assert.Equal(JsonValueKind.Null, local.GetProperty("serverItemId").ValueKind);
        await phone.Page.Context.SetOfflineAsync(false);
        await AssertReadyAsync(phone);
        await AssertOneItemAndPhotoAsync(phone);
    }

    private static async Task PrepareCaptureAsync(PhoneSession phone)
    {
        var project = await phone.ProjectIdAsync("HB-TEST-001");
        await phone.GotoAsync($"/projects/{project}/new-item");
        await phone.Page.GetByRole(AriaRole.Combobox, new() { Name = "Where?" }).FillAsync("Unit 214");
        await phone.Page.GetByRole(AriaRole.Button, new() { Name = "Drywall", Exact = true }).ClickAsync();
        await phone.Page.GetByText("Punch List", new() { Exact = true }).ClickAsync();
    }

    private static async Task ChooseSyntheticPhotoAsync(IPage page)
    {
        // Generated geometry only. Large enough to exercise actual worker resizing and JPEG encoding.
        var base64 = await page.EvaluateAsync<string>("""
            () => {
              const canvas = document.createElement('canvas');
              canvas.width = 3200; canvas.height = 2400;
              const c = canvas.getContext('2d');
              c.fillStyle = '#dddddd'; c.fillRect(0, 0, 3200, 2400);
              c.fillStyle = '#202020'; c.fillRect(300, 300, 40, 1700);
              c.fillStyle = '#b02020'; c.fillRect(340, 1200, 500, 30);
              return canvas.toDataURL('image/png').split(',')[1];
            }
            """);
        await page.GetByTestId("library-input").SetInputFilesAsync(new FilePayload
        {
            Name = "synthetic-inspection.png",
            MimeType = "image/png",
            Buffer = Convert.FromBase64String(base64),
        });
        await Expect(page.GetByRole(AriaRole.Dialog, new() { Name = "Photo preview" })).ToBeVisibleAsync();
    }

    private static async Task AssertReadyAsync(PhoneSession phone) =>
        await Expect(phone.Page.Locator("[data-status='ReadyForDescription']")).ToBeVisibleAsync(new() { Timeout = 30_000 });

    private static async Task AssertOneItemAndPhotoAsync(PhoneSession phone)
    {
        var local = await phone.Page.EvaluateAsync<JsonElement>(DraftScript);
        var itemId = local.GetProperty("serverItemId").GetString();
        var headers = new Dictionary<string, string> { ["X-Dev-Persona"] = "superintendent" };
        var response = await phone.Api.GetAsync($"/api/v1/items/{itemId}", new() { Headers = headers });
        Assert.True(response.Ok);
        var item = (await response.JsonAsync())!.Value;
        var photos = item.GetProperty("photos").EnumerateArray().ToArray();
        var photo = Assert.Single(photos);
        Assert.Equal("Finalized", photo.GetProperty("status").GetString());
        Assert.Equal(2560, photo.GetProperty("width").GetInt32());
        Assert.Equal(local.GetProperty("serverPhotoId").GetString(), photo.GetProperty("id").GetString());
        var thumb = await phone.Api.GetAsync(photo.GetProperty("thumbnailUrl").GetString()!, new() { Headers = headers });
        Assert.True(thumb.Ok);
        Assert.Equal("image/jpeg", thumb.Headers["content-type"]);
        headers["Idempotency-Key"] = local.GetProperty("clientDraftId").GetString()!;
        var replay = await phone.Api.PostAsync($"/api/v1/projects/{local.GetProperty("projectId").GetString()}/items", new()
        {
            Headers = headers,
            DataObject = new
            {
                clientDraftId = local.GetProperty("clientDraftId").GetString(),
                type = local.GetProperty("itemType").GetString(),
                locationDetail = local.GetProperty("locationDetail").GetString(),
                tradeId = local.GetProperty("tradeId").GetString(),
            },
        });
        Assert.True(replay.Ok);
        Assert.Equal(itemId, (await replay.JsonAsync())!.Value.GetProperty("id").GetString());
        Assert.Equal("true", replay.Headers["idempotent-replayed"]);
    }
}
