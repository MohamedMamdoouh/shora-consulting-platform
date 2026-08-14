import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { BookingFlowStateService } from '../booking-flow-state.service';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { formatSlotSummary } from '../utils/slot-grouping.util';
import { BookingStepIndicatorComponent } from '../shared/booking-step-indicator.component';

const DIGITS_MAX11_PATTERN = /^\d{1,11}$/;

@Component({
  selector: 'app-contact-phone',
  imports: [ReactiveFormsModule, RouterLink, BookingStepIndicatorComponent],
  templateUrl: './contact-phone.component.html',
  styleUrl: './contact-phone.component.scss',
})
export class ContactPhoneComponent {
  private readonly fb = inject(FormBuilder);
  private readonly bookingFlow = inject(BookingFlowStateService);
  private readonly router = inject(Router);

  protected readonly copy = APP_COPY;

  readonly slotSummary = formatSlotSummary(this.bookingFlow.getState()!.slotStartUtc);

  readonly form = this.fb.nonNullable.group({
    contactPhone: [
      this.bookingFlow.getState()?.contactPhone ?? '',
      [Validators.required, Validators.pattern(DIGITS_MAX11_PATTERN), Validators.maxLength(11)],
    ],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.bookingFlow.setContactPhone(this.form.controls.contactPhone.value.trim());
    void this.router.navigate(['/booking/review']);
  }

  phoneError(): string | null {
    const control = this.form.controls.contactPhone;
    if (!control.touched || !control.invalid) {
      return null;
    }

    if (control.hasError('required')) {
      return 'رقم الهاتف مطلوب للمكالمة الصوتية.';
    }

    return 'أدخل رقم موبايل مصري صالح.';
  }
}
