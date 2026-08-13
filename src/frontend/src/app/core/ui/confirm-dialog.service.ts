import { Injectable, signal } from '@angular/core';

export type ConfirmDialogVariant = 'default' | 'danger';

export interface ConfirmDialogOptions {
  title?: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: ConfirmDialogVariant;
}

export interface ConfirmDialogRequest extends ConfirmDialogOptions {
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
        variant: options.variant ?? 'default',
        resolve,
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
