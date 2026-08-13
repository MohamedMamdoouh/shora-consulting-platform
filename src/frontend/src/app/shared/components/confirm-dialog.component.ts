import {
  afterRenderEffect,
  Component,
  ElementRef,
  inject,
  viewChild,
  ViewEncapsulation,
} from '@angular/core';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { ConfirmDialogService } from '../../core/ui/confirm-dialog.service';

@Component({
  selector: 'app-confirm-dialog',
  encapsulation: ViewEncapsulation.None,
  template: `
    <dialog
      #dialogEl
      class="confirm-dialog"
      [attr.closedby]="'any'"
      aria-labelledby="confirm-dialog-title"
      aria-describedby="confirm-dialog-message"
      (click)="onDialogClick($event)"
      (close)="onNativeClose()"
    >
      @if (dialog.request(); as request) {
        <div
          class="confirm-dialog__card"
          [class.confirm-dialog__card--danger]="request.variant === 'danger'"
        >
          <h2 id="confirm-dialog-title" class="confirm-dialog__title">
            {{ request.title || copy.dialog.defaultTitle }}
          </h2>
          <p id="confirm-dialog-message" class="confirm-dialog__message">{{ request.message }}</p>
          <div class="confirm-dialog__actions">
            <button
              type="button"
              class="btn"
              [class.btn--danger]="request.variant === 'danger'"
              [attr.autofocus]="request.variant === 'danger' ? null : ''"
              (click)="accept()"
            >
              {{ request.confirmLabel || copy.dialog.confirm }}
            </button>
            <button
              type="button"
              class="btn btn--secondary"
              [attr.autofocus]="request.variant === 'danger' ? '' : null"
              (click)="dismiss()"
            >
              {{ request.cancelLabel || copy.dialog.cancel }}
            </button>
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
      gap: var(--space-lg);
      width: min(100%, 24rem);
      padding: var(--space-xl);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-xl);
      background: var(--color-surface);
      box-shadow: var(--shadow-lg);
      animation: confirm-card-in var(--transition-base) both;
    }

    .confirm-dialog__title {
      position: relative;
      margin: 0;
      padding-inline-start: var(--space-lg);
      font-family: var(--font-display);
      font-size: var(--font-size-lg);
      font-weight: 700;
      line-height: var(--line-height-heading);
    }

    .confirm-dialog__title::before {
      content: '';
      position: absolute;
      inset-inline-start: 0;
      top: 0.15em;
      bottom: 0.15em;
      width: 4px;
      border-radius: var(--radius-full);
      background: var(--color-primary);
    }

    .confirm-dialog__card--danger .confirm-dialog__title::before {
      background: var(--color-error);
    }

    .confirm-dialog__message {
      margin: 0;
      color: var(--color-text-muted);
      font-size: var(--font-size-base);
      line-height: var(--line-height-body);
    }

    .confirm-dialog__actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-sm);
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
      if (!request || !element || element.open || typeof element.showModal !== 'function') {
        return;
      }

      element.showModal();
    });
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
