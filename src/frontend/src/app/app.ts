import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConfirmDialogComponent } from './shared/components/confirm-dialog.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ConfirmDialogComponent],
  template: `
    <router-outlet />
    <app-confirm-dialog />
  `,
  styles: ':host { display: block; min-height: 100vh; }'
})
export class App {}
