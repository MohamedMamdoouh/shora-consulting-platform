import { Routes } from '@angular/router';
import { PlaceholderPageComponent } from '../shared/components/placeholder-page.component';

export const BOOKING_ROUTES: Routes = [
  {
    path: 'start',
    component: PlaceholderPageComponent,
    data: { title: 'حجز جلسة', message: 'مسار الحجز — المواصفة 04.' }
  },
  { path: '', redirectTo: 'start', pathMatch: 'full' }
];
