import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { BookingFlowStateService } from '../booking-flow-state.service';
import { formatSlotSummary } from '../utils/slot-grouping.util';

const EGYPT_MOBILE_PATTERN = /^(\+20|0)?1[0125]\d{8}$/;

@Component({
  selector: 'app-contact-phone',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './contact-phone.component.html',
  styleUrl: './contact-phone.component.scss',
})
export class ContactPhoneComponent {
  private readonly fb = inject(FormBuilder);
  private readonly bookingFlow = inject(BookingFlowStateService);
  private readonly router = inject(Router);

  readonly slotSummary = formatSlotSummary(this.bookingFlow.getState()!.slotStartUtc);

  readonly form = this.fb.nonNullable.group({
    contactPhone: [
      this.bookingFlow.getState()?.contactPhone ?? '',
      [Validators.required, Validators.pattern(EGYPT_MOBILE_PATTERN)],
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
