import { Routes } from '@angular/router';
import { clientGuard } from '../core/auth/auth.guard';
import {
  bookingPhoneGuard,
  bookingReviewGuard,
  bookingSlotSelectedGuard,
} from './booking.guards';
import { ContactPhoneComponent } from './contact-phone/contact-phone.component';
import { DeliveryMethodComponent } from './delivery-method/delivery-method.component';
import { PaymentInstructionsComponent } from './payment-instructions/payment-instructions.component';
import { ReviewComponent } from './review/review.component';
import { SlotPickerComponent } from './slot-picker/slot-picker.component';
export const BOOKING_ROUTES: Routes = [
  {
    path: 'start',
    component: SlotPickerComponent,
  },
  {
    path: 'delivery',
    component: DeliveryMethodComponent,
    canActivate: [bookingSlotSelectedGuard],
  },
  {
    path: 'phone',
    component: ContactPhoneComponent,
    canActivate: [bookingPhoneGuard],
  },
  {
    path: 'review',
    component: ReviewComponent,
    canActivate: [bookingReviewGuard],
  },
  {
    path: 'payment/:id',
    component: PaymentInstructionsComponent,
    canActivate: [clientGuard],
  },
  { path: '', redirectTo: 'start', pathMatch: 'full' },
];
