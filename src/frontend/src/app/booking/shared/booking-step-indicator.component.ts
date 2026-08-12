import { Component, input } from '@angular/core';

export type BookingStep = 'slot' | 'delivery' | 'phone' | 'review' | 'payment';

interface StepDef {
  id: BookingStep;
  label: string;
}

const STEPS: StepDef[] = [
  { id: 'slot', label: 'الموعد' },
  { id: 'delivery', label: 'التواصل' },
  { id: 'phone', label: 'الهاتف' },
  { id: 'review', label: 'المراجعة' },
  { id: 'payment', label: 'الدفع' },
];

@Component({
  selector: 'app-booking-step-indicator',
  template: `
    <nav class="step-indicator" aria-label="خطوات الحجز">
      <ol class="step-indicator__list">
        @for (step of visibleSteps(); track step.id; let index = $index) {
          <li
            class="step-indicator__item"
            [class.step-indicator__item--active]="step.id === currentStep()"
            [class.step-indicator__item--done]="isDone(step.id)"
          >
            <span class="step-indicator__marker" aria-hidden="true">
              @if (isDone(step.id)) {
                ✓
              } @else {
                {{ index + 1 }}
              }
            </span>
            <span class="step-indicator__label">{{ step.label }}</span>
          </li>
        }
      </ol>
    </nav>
  `,
  styles: `
    .step-indicator {
      margin-bottom: var(--space-lg);
    }

    .step-indicator__list {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-sm);
      list-style: none;
      margin: 0;
      padding: var(--space-sm);
      background: var(--color-paper);
      border: 1px solid var(--color-border-subtle);
      border-radius: var(--radius-lg);
    }

    .step-indicator__item {
      display: flex;
      align-items: center;
      gap: var(--space-sm);
      flex: 1 1 auto;
      min-width: 0;
      padding: var(--space-sm) var(--space-md);
      border-radius: var(--radius-md);
      color: var(--color-text-muted);
      font-size: var(--font-size-xs);
      font-weight: 600;
    }

    .step-indicator__item--active {
      background: var(--color-surface-elevated);
      color: var(--color-primary);
      box-shadow: var(--shadow-sm);
    }

    .step-indicator__item--done {
      color: var(--color-success);
    }

    .step-indicator__marker {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      width: 1.5rem;
      height: 1.5rem;
      border-radius: var(--radius-full);
      background: var(--color-border-subtle);
      font-size: 0.6875rem;
      font-weight: 700;
    }

    .step-indicator__item--active .step-indicator__marker {
      background: var(--color-primary);
      color: #fff;
    }

    .step-indicator__item--done .step-indicator__marker {
      background: var(--color-success-bg);
      color: var(--color-success);
      border: 1px solid var(--color-success-border);
    }

    .step-indicator__label {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    @media (max-width: 480px) {
      .step-indicator__list {
        flex-direction: column;
      }

      .step-indicator__item {
        flex: none;
      }
    }
  `,
})
export class BookingStepIndicatorComponent {
  readonly currentStep = input.required<BookingStep>();
  readonly showPhoneStep = input(true);

  protected visibleSteps(): StepDef[] {
    if (this.showPhoneStep()) {
      return STEPS;
    }
    return STEPS.filter((step) => step.id !== 'phone');
  }

  protected isDone(stepId: BookingStep): boolean {
    const order = this.visibleSteps().map((s) => s.id);
    const currentIndex = order.indexOf(this.currentStep());
    const stepIndex = order.indexOf(stepId);
    return stepIndex >= 0 && stepIndex < currentIndex;
  }
}
