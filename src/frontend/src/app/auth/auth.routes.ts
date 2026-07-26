import { Routes } from '@angular/router';
import { ForgotPasswordPageComponent } from './forgot-password-page.component';
import { LoginPageComponent } from './login-page.component';
import { ResetPasswordPageComponent } from './reset-password-page.component';
import { SignupPageComponent } from './signup-page.component';
import { VerifyEmailPageComponent } from './verify-email-page.component';

export const AUTH_ROUTES: Routes = [
  { path: 'login', component: LoginPageComponent },
  { path: 'signup', component: SignupPageComponent },
  { path: 'verify-email', component: VerifyEmailPageComponent },
  { path: 'forgot-password', component: ForgotPasswordPageComponent },
  { path: 'reset-password', component: ResetPasswordPageComponent },
  { path: '', redirectTo: 'login', pathMatch: 'full' }
];
