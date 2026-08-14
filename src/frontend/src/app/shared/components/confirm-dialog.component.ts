import {
  afterRenderEffect,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  signal,
  viewChild,
  ViewEncapsulation,
} from '@angular/core';
import { Router } from '@angular/router';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import {
  ConfirmDialogRequest,
  ConfirmDialogService,
  DEFAULT_RESULT_DIALOG_TIMEOUT_MS,
} from '../../core/ui/confirm-dialog.service';

@Component({
  selector: 'app-confirm-dialog',
  encapsulation: ViewEncapsulation.None,
  template: `
    <dialog
      #dialogEl
      class="confirm-dialog"
      [attr.closedby]="request()?.mode === 'result' ? null : 'any'"
      [attr.role]="dialogRole()"
      aria-labelledby="confirm-dialog-title"
      aria-describedby="confirm-dialog-message"
      (click)="onDialogClick($event)"
      (close)="onNativeClose()"
    >
      @if (request(); as current) {
        <div
          class="confirm-dialog__card"
          [class.confirm-dialog__card--danger]="current.variant === 'danger'"
          [class.confirm-dialog__card--success]="current.variant === 'success'"
        >
          <div class="confirm-dialog__icon" aria-hidden="true">
            @switch (current.variant) {
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
            {{ titleFor(current) }}
          </h2>
          <p id="confirm-dialog-message" class="confirm-dialog__message">{{ current.message }}</p>
          @if (current.detail) {
            <p class="confirm-dialog__detail">{{ current.detail }}</p>
          }
          @if (current.mode === 'result') {
            <p class="confirm-dialog__countdown" aria-live="polite">
              {{ countdownLabel(current) }}
            </p>
          }
          <div class="confirm-dialog__actions">
            <button
              type="button"
              class="btn"
              [class.btn--danger]="current.variant === 'danger'"
              [attr.autofocus]="current.variant === 'danger' ? null : ''"
              (click)="accept()"
            >
              {{ confirmLabelFor(current) }}
            </button>
            @if (current.mode === 'confirm') {
              <button
                type="button"
                class="btn btn--secondary"
                [attr.autofocus]="current.variant === 'danger' ? '' : null"
                (click)="dismiss()"
              >
                {{ current.cancelLabel || copy.dialog.cancel }}
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

    .confirm-dialog__countdown {
      margin: 0;
      width: 100%;
      padding: var(--space-sm) var(--space-md);
      border-radius: var(--radius-md);
      background: var(--color-background);
      color: var(--color-text-muted);
      font-size: var(--font-size-sm);
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
export class ConfirmDialogComponent implements OnDestroy {
  private readonly dialogEl = viewChild<ElementRef<HTMLDialogElement>>('dialogEl');
  private readonly router = inject(Router);
  private pendingResult: boolean | null = null;
  private countdownTimer: ReturnType<typeof setInterval> | null = null;
  private completingResult = false;

  protected readonly dialog = inject(ConfirmDialogService);
  protected readonly copy = APP_COPY;
  protected readonly request = this.dialog.request;
  protected readonly secondsLeft = signal(0);

  constructor() {
    afterRenderEffect(() => {
      const current = this.request();
      const element = this.dialogEl()?.nativeElement;
      if (!element || typeof element.showModal !== 'function') {
        return;
      }

      if (current && !element.open) {
        element.showModal();
        if (current.mode === 'result') {
          this.startResultCountdown(current);
        }
        return;
      }

      if (!current && element.open) {
        this.clearResultCountdown();
        element.close();
      }
    });
  }

  ngOnDestroy(): void {
    this.clearResultCountdown();
  }

  protected dialogRole(): 'alertdialog' | 'dialog' {
    const mode = this.request()?.mode;
    return mode === 'alert' || mode === 'result' ? 'alertdialog' : 'dialog';
  }

  protected titleFor(current: ConfirmDialogRequest): string {
    if (current.title) {
      return current.title;
    }

    if (current.mode === 'result' && current.variant === 'danger') {
      return this.copy.dialog.errorTitle;
    }

    return current.mode === 'alert' || current.mode === 'result'
      ? this.copy.dialog.successTitle
      : this.copy.dialog.defaultTitle;
  }

  protected confirmLabelFor(current: ConfirmDialogRequest): string {
    if (current.confirmLabel) {
      return current.confirmLabel;
    }

    return current.mode === 'alert' || current.mode === 'result'
      ? this.copy.dialog.acknowledge
      : this.copy.dialog.confirm;
  }

  protected countdownLabel(current: ConfirmDialogRequest): string {
    const seconds = this.secondsLeft();
    return current.redirectTo
      ? this.copy.dialog.redirectingIn(seconds)
      : this.copy.dialog.closingIn(seconds);
  }

  protected accept(): void {
    void this.closeWith(true);
  }

  protected dismiss(): void {
    void this.closeWith(false);
  }

  protected onNativeClose(): void {
    const current = this.request();
    const confirmed = this.pendingResult ?? (current?.mode === 'result' ? true : false);
    this.pendingResult = null;

    if (current?.mode === 'result' && confirmed) {
      void this.finishResult(current);
      return;
    }

    this.dialog.settle(confirmed);
  }

  protected onDialogClick(event: MouseEvent): void {
    if (event.target !== event.currentTarget) {
      return;
    }

    const current = this.request();
    if (current?.mode === 'result') {
      void this.accept();
      return;
    }

    void this.dismiss();
  }

  private startResultCountdown(current: ConfirmDialogRequest): void {
    this.clearResultCountdown();

    const timeoutMs = current.timeoutMs ?? DEFAULT_RESULT_DIALOG_TIMEOUT_MS;
    const totalSeconds = Math.max(1, Math.ceil(timeoutMs / 1000));
    this.secondsLeft.set(totalSeconds);

    this.countdownTimer = setInterval(() => {
      const next = this.secondsLeft() - 1;
      if (next <= 0) {
        void this.accept();
        return;
      }

      this.secondsLeft.set(next);
    }, 1000);
  }

  private clearResultCountdown(): void {
    if (this.countdownTimer !== null) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }

  private async closeWith(confirmed: boolean): Promise<void> {
    this.pendingResult = confirmed;
    const element = this.dialogEl()?.nativeElement;
    if (element?.open) {
      element.close();
      return;
    }

    this.onNativeClose();
  }

  private async finishResult(current: ConfirmDialogRequest): Promise<void> {
    if (this.completingResult) {
      return;
    }

    this.completingResult = true;
    this.clearResultCountdown();

    try {
      if (current.onComplete) {
        await current.onComplete();
      }

      if (current.redirectTo) {
        const commands = Array.isArray(current.redirectTo)
          ? current.redirectTo
          : [current.redirectTo];
        await this.router.navigate(commands);
      }
    } finally {
      this.completingResult = false;
      this.dialog.settle(true);
    }
  }
}
