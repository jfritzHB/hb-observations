import { useEffect, useId, useRef, type ReactNode } from 'react';
import { CloseIcon } from './icons';

interface SheetProps {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
}

/**
 * Full-height bottom sheet built on the native modal <dialog>: focus is trapped, Escape closes it, and focus
 * returns to the control that opened it.
 */
export function Sheet({ open, title, onClose, children }: SheetProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const returnFocusRef = useRef<HTMLElement | null>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) {
      return;
    }

    if (open && !dialog.open) {
      returnFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  const handleClose = () => {
    returnFocusRef.current?.focus();
    onClose();
  };

  return (
    <dialog ref={dialogRef} className="sheet" aria-labelledby={titleId} onClose={handleClose}>
      <div className="sheet__header">
        <h2 id={titleId} className="sheet__title">
          {title}
        </h2>
        <button
          type="button"
          className="icon-button"
          onClick={handleClose}
          aria-label={`Close ${title.toLowerCase()}`}
        >
          <CloseIcon />
        </button>
      </div>
      <div className="sheet__body">{open ? children : null}</div>
    </dialog>
  );
}
