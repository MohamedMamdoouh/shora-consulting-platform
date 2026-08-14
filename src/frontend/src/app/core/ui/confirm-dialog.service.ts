import { Injectable, signal } from '@angular/core';

export type ConfirmDialogMode = 'confirm' | 'alert' | 'result';
export type ConfirmDialogVariant = 'default' | 'danger' | 'success';

export interface ConfirmDialogOptions {
  title?: string;
  message: string;
  detail?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: ConfirmDialogVariant;
  mode?: ConfirmDialogMode;
}

export interface ResultDialogOptions {
  title?: string;
  message: string;
  detail?: string;
  confirmLabel?: string;
  variant?: ConfirmDialogVariant;
  timeoutMs?: number;
  redirectTo?: string | readonly string[] | null;
  onComplete?: () => void | Promise<void>;
}

export interface ConfirmDialogRequest extends ConfirmDialogOptions {
  readonly mode: ConfirmDialogMode;
  readonly variant: ConfirmDialogVariant;
  readonly timeoutMs?: number;
  readonly redirectTo?: string | readonly string[] | null;
  readonly onComplete?: () => void | Promise<void>;
  readonly resolve: (confirmed: boolean) => void;
}

export const DEFAULT_RESULT_DIALOG_TIMEOUT_MS = 5000;

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly requestState = signal<ConfirmDialogRequest | null>(null);

  readonly request = this.requestState.asReadonly();

  confirm(options: ConfirmDialogOptions): Promise<boolean> {
    this.settle(false);

    return new Promise((resolve) => {
      this.requestState.set({
        ...options,
        mode: options.mode ?? 'confirm',
        variant: options.variant ?? 'default',
        resolve,
      });
    });
  }

  async alert(options: ConfirmDialogOptions): Promise<void> {
    await this.confirm({
      ...options,
      mode: 'alert',
      variant: options.variant ?? 'success',
    });
  }

  result(options: ResultDialogOptions): Promise<void> {
    this.settle(false);

    return new Promise((resolve) => {
      this.requestState.set({
        title: options.title,
        message: options.message,
        detail: options.detail,
        confirmLabel: options.confirmLabel,
        mode: 'result',
        variant: options.variant ?? 'success',
        timeoutMs: options.timeoutMs ?? DEFAULT_RESULT_DIALOG_TIMEOUT_MS,
        redirectTo: options.redirectTo ?? null,
        onComplete: options.onComplete,
        resolve: () => {
          resolve();
        },
      });
    });
  }

  settle(confirmed: boolean): void {
    const pending = this.requestState();
    if (!pending) {
      return;
    }

    this.requestState.set(null);
    pending.resolve(confirmed);
  }
}
