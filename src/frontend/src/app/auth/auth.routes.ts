import { Routes } from '@angular/router';
import { PlaceholderPageComponent } from '../shared/components/placeholder-page.component';

export const AUTH_ROUTES: Routes = [
  {
    path: 'login',
    component: PlaceholderPageComponent,
    data: { title: 'تسجيل الدخول', message: 'المصادقة — المواصفة 02.' }
  },
  { path: '', redirectTo: 'login', pathMatch: 'full' }
];
