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
  templateUrl: './booking-step-indicator.component.html',
  styleUrl: './booking-step-indicator.component.scss',
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
