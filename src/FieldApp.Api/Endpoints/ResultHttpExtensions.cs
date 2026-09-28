using FieldApp.Application.Common;

namespace FieldApp.Api.Endpoints;

/// <summary>Translates application results into HTTP responses; every failure is RFC 7807 Problem Details.</summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return onSuccess is null ? TypedResults.Ok(result.Value) : onSuccess(result.Value!);
        }

        return result.Error!.ToHttp();
    }

    public static IResult ToHttp(this AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Kind switch
        {
            ErrorKind.Validation => TypedResults.ValidationProblem(
                error.ValidationErrors ?? new Dictionary<string, string[]>(),
                title: error.Title),
            ErrorKind.NotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: error.Title),
            ErrorKind.Forbidden => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: error.Title),
            ErrorKind.Conflict => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: error.Title),
            ErrorKind.PayloadTooLarge => TypedResults.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: error.Title),
            ErrorKind.UnsupportedMediaType => TypedResults.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: error.Title),
            ErrorKind.Unprocessable => TypedResults.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: error.Title),
            ErrorKind.Unavailable => TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: error.Title),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Kind, "Unknown error kind."),
        };
    }
}
