import {
  afterRenderEffect,
  Component,
  ElementRef,
  inject,
  viewChild,
  ViewEncapsulation,
} from '@angular/core';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import {
  ConfirmDialogRequest,
  ConfirmDialogService,
} from '../../core/ui/confirm-dialog.service';

@Component({
  selector: 'app-confirm-dialog',
  encapsulation: ViewEncapsulation.None,
  template: `
    <dialog
      #dialogEl
      class="confirm-dialog"
      [attr.closedby]="'any'"
      [attr.role]="dialog.request()?.mode === 'alert' ? 'alertdialog' : 'dialog'"
      aria-labelledby="confirm-dialog-title"
      aria-describedby="confirm-dialog-message"
      (click)="onDialogClick($event)"
      (close)="onNativeClose()"
    >
      @if (dialog.request(); as request) {
        <div
          class="confirm-dialog__card"
          [class.confirm-dialog__card--danger]="request.variant === 'danger'"
          [class.confirm-dialog__card--success]="request.variant === 'success'"
        >
          <div class="confirm-dialog__icon" aria-hidden="true">
            @switch (request.variant) {
              @case ('danger') {
                <svg viewBox="0 0 24 24" width="28" height="28" fill="none">
                  <path
                    d="M4 7h16M9 7V5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2m-9 0 1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  />
                </svg>
              }
              @case ('success') {
                <svg viewBox="0 0 24 24" width="28" height="28" fill="none">
                  <path
                    d="M20 7 10 17l-6-6"
                    stroke="currentColor"
                    stroke-width="2.2"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  />
                </svg>
              }
              @default {
                <svg viewBox="0 0 24 24" width="28" height="28" fill="none">
                  <circle cx="12" cy="12" r="8.25" stroke="currentColor" stroke-width="1.8" />
                  <path
                    d="M12 11v5M12 8h.01"
                    stroke="currentColor"
                    stroke-width="1.8"
                    stroke-linecap="round"
                  />
                </svg>
              }
            }
          </div>
          <h2 id="confirm-dialog-title" class="confirm-dialog__title">
            {{ titleFor(request) }}
          </h2>
          <p id="confirm-dialog-message" class="confirm-dialog__message">{{ request.message }}</p>
          @if (request.detail) {
            <p class="confirm-dialog__detail">{{ request.detail }}</p>
          }
          <div class="confirm-dialog__actions">
            <button
              type="button"
              class="btn"
              [class.btn--danger]="request.variant === 'danger'"
              [attr.autofocus]="request.variant === 'danger' ? null : ''"
              (click)="accept()"
            >
              {{ confirmLabelFor(request) }}
            </button>
            @if (request.mode !== 'alert') {
              <button
                type="button"
                class="btn btn--secondary"
                [attr.autofocus]="request.variant === 'danger' ? '' : null"
                (click)="dismiss()"
              >
                {{ request.cancelLabel || copy.dialog.cancel }}
              </button>
            }
          </div>
        </div>
      }
    </dialog>
  `,
  styles: `
    :host {
      display: contents;
    }

    .confirm-dialog {
      inset: 0;
      width: 100%;
      max-width: none;
      height: 100%;
      max-height: none;
      margin: 0;
      padding: var(--space-lg);
      border: none;
      background: transparent;
      color: inherit;
      overflow: auto;
    }

    .confirm-dialog[open] {
      display: grid;
      place-items: center;
    }

    .confirm-dialog::backdrop {
      background: var(--color-overlay);
    }

    .confirm-dialog__card {
      display: grid;
      justify-items: start;
      gap: var(--space-md);
      width: min(100%, 24rem);
      padding: var(--space-xl);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-xl);
      background: var(--color-surface);
      box-shadow: var(--shadow-lg);
      animation: confirm-card-in var(--transition-base) both;
    }

    .confirm-dialog__icon {
      display: grid;
      place-items: center;
      width: 3rem;
      height: 3rem;
      border-radius: var(--radius-full);
      background: var(--color-primary-soft);
      color: var(--color-primary);
    }

    .confirm-dialog__card--danger .confirm-dialog__icon {
      background: var(--color-error-bg);
      color: var(--color-error);
    }

    .confirm-dialog__card--success .confirm-dialog__icon {
      background: var(--color-success-bg);
      color: var(--color-success);
    }

    .confirm-dialog__title {
      margin: 0;
      font-family: var(--font-display);
      font-size: var(--font-size-lg);
      font-weight: 700;
      line-height: var(--line-height-heading);
    }

    .confirm-dialog__message {
      margin: 0;
      color: var(--color-text-muted);
      font-size: var(--font-size-base);
      line-height: var(--line-height-body);
    }

    .confirm-dialog__detail {
      margin: 0;
      width: 100%;
      padding: var(--space-sm) var(--space-md);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-linen);
      color: var(--color-text);
      font-weight: 600;
    }

    .confirm-dialog__card--danger .confirm-dialog__detail {
      border-color: var(--color-error-border);
      background: var(--color-error-bg);
    }

    .confirm-dialog__actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-sm);
      width: 100%;
      margin-top: var(--space-xs);
    }

    @keyframes confirm-card-in {
      from {
        opacity: 0;
        transform: translateY(10px);
      }

      to {
        opacity: 1;
        transform: none;
      }
    }
  `,
})
export class ConfirmDialogComponent {
  private readonly dialogEl = viewChild<ElementRef<HTMLDialogElement>>('dialogEl');
  private pendingResult: boolean | null = null;

  protected readonly dialog = inject(ConfirmDialogService);
  protected readonly copy = APP_COPY;

  constructor() {
    afterRenderEffect(() => {
      const request = this.dialog.request();
      const element = this.dialogEl()?.nativeElement;
      if (!element || typeof element.showModal !== 'function') {
        return;
      }

      if (request && !element.open) {
        element.showModal();
        return;
      }

      if (!request && element.open) {
        element.close();
      }
    });
  }

  protected titleFor(request: ConfirmDialogRequest): string {
    return (
      request.title ||
      (request.mode === 'alert' ? this.copy.dialog.successTitle : this.copy.dialog.defaultTitle)
    );
  }

  protected confirmLabelFor(request: ConfirmDialogRequest): string {
    return (
      request.confirmLabel ||
      (request.mode === 'alert' ? this.copy.dialog.acknowledge : this.copy.dialog.confirm)
    );
  }

  protected accept(): void {
    this.closeWith(true);
  }

  protected dismiss(): void {
    this.closeWith(false);
  }

  protected onNativeClose(): void {
    const result = this.pendingResult ?? false;
    this.pendingResult = null;
    this.dialog.settle(result);
  }

  protected onDialogClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.dismiss();
    }
  }

  private closeWith(confirmed: boolean): void {
    this.pendingResult = confirmed;
    const element = this.dialogEl()?.nativeElement;
    if (element?.open) {
      element.close();
      return;
    }

    this.onNativeClose();
  }
}
