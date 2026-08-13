import { Injectable, signal } from '@angular/core';

export type ConfirmDialogMode = 'confirm' | 'alert';
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

export interface ConfirmDialogRequest extends ConfirmDialogOptions {
  readonly mode: ConfirmDialogMode;
  readonly variant: ConfirmDialogVariant;
  readonly resolve: (confirmed: boolean) => void;
}

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

  settle(confirmed: boolean): void {
    const pending = this.requestState();
    if (!pending) {
      return;
    }

    this.requestState.set(null);
    pending.resolve(confirmed);
  }
}
