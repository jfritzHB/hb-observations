import type { CaptureDraft } from './draftModel';

/**
 * Plain-language status for a draft. A local-only draft is never described as saved to the server
 * (CLAUDE.md), and technical upload terms are avoided.
 */
export interface DraftStatusView {
  tone: 'progress' | 'ready' | 'problem';
  /** Where the capture currently lives. */
  where: 'On this device only' | 'Saved to server';
  title: string;
  detail: string;
}

export function describeDraftStatus(draft: CaptureDraft, online: boolean): DraftStatusView {
  switch (draft.status) {
    case 'ReadyForDescription':
      return {
        tone: 'ready',
        where: 'Saved to server',
        title: 'Saved to the server as a draft',
        detail: 'Your description is kept on this device until the item is saved.',
      };
    case 'PhotoCaptured':
    case 'CreatingServerDraft':
      return {
        tone: 'progress',
        where: draft.serverItemId ? 'Saved to server' : 'On this device only',
        title: 'Saving…',
        detail: 'Your photo is safe on this device while it is sent to the server.',
      };
    case 'Uploading':
      return {
        tone: 'progress',
        where: 'On this device only',
        title: 'Sending photo…',
        detail: 'Your photo is safe on this device while it is sent.',
      };
    case 'Finalizing':
      return {
        tone: 'progress',
        where: 'On this device only',
        title: 'Checking the photo…',
        detail: 'The server is checking the photo it received.',
      };
    case 'Failed': {
      const failure = draft.failure;
      if (failure?.kind === 'offline' || !online) {
        return {
          tone: 'problem',
          where: 'On this device only',
          title: 'Not sent yet: no connection',
          detail:
            'Your photo is safe on this device. It will be sent when you are back online, or tap Try again.',
        };
      }

      if (failure?.kind === 'rejected') {
        return { tone: 'problem', where: 'On this device only', title: 'Not sent', detail: failure.message };
      }

      return {
        tone: 'problem',
        where: 'On this device only',
        title: 'Not sent yet',
        detail: `${failure?.message ?? 'Something went wrong.'} Your photo is safe on this device.`,
      };
    }
  }
}

/** Short label for lists. */
export function draftListLabel(draft: CaptureDraft): string {
  switch (draft.status) {
    case 'ReadyForDescription':
      return 'Saved to server · needs description';
    case 'Failed':
      return 'Not sent yet · on this device';
    default:
      return 'Sending…';
  }
}
