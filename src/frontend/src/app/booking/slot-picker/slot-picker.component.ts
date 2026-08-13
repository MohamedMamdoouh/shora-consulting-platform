import { Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { AvailabilitySlot } from '@contracts/availability';
import { catchError, combineLatest, map, Observable, of, startWith, Subject, switchMap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { AvailabilityService } from '../../core/availability/availability.service';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { BookingFlowStateService } from '../booking-flow-state.service';
import { isAdminBlockedFromBooking } from '../booking-guard-decisions.util';
import { formatSlotTime, groupSlotsByLocalDay, SlotDayGroup } from '../utils/slot-grouping.util';
import { BookingStepIndicatorComponent } from '../shared/booking-step-indicator.component';

type SlotPickerViewModel =
  | { status: 'loading'; groups: SlotDayGroup[] }
  | { status: 'ready'; groups: SlotDayGroup[] }
  | { status: 'error'; groups: SlotDayGroup[] };

const initialViewModel: SlotPickerViewModel = { status: 'loading', groups: [] };

@Component({
  selector: 'app-slot-picker',
  imports: [BookingStepIndicatorComponent, RouterLink],
  templateUrl: './slot-picker.component.html',
  styleUrl: './slot-picker.component.scss',
})
export class SlotPickerComponent {
  private readonly auth = inject(AuthService);
  private readonly availabilityService = inject(AvailabilityService);
  private readonly bookingFlow = inject(BookingFlowStateService);
  private readonly router = inject(Router);
  private readonly reload$ = new Subject<void>();

  protected readonly copy = APP_COPY;
  readonly formatSlotTime = formatSlotTime;
  protected readonly isAdminBlocked = computed(() =>
    isAdminBlockedFromBooking(this.auth.currentUser()?.role),
  );

  private readonly viewModel$ = combineLatest([
    toObservable(this.isAdminBlocked),
    this.reload$.pipe(startWith(undefined)),
  ]).pipe(
    switchMap(([blocked]) => {
      if (blocked) {
        return of({ status: 'ready', groups: [] as SlotDayGroup[] } as SlotPickerViewModel);
      }

      return this.availabilityService.getAvailability().pipe(
        map((response): SlotPickerViewModel => ({
          status: 'ready',
          groups: groupSlotsByLocalDay(response.slots),
        })),
        catchError((): Observable<SlotPickerViewModel> =>
          of({ status: 'error', groups: [] as SlotDayGroup[] }),
        ),
        startWith({ status: 'loading', groups: [] as SlotDayGroup[] } as SlotPickerViewModel),
      );
    }),
  );

  readonly viewModel = toSignal(this.viewModel$, { initialValue: initialViewModel });

  reload(): void {
    this.reload$.next();
  }

  selectSlot(slot: AvailabilitySlot): void {
    if (this.isAdminBlocked()) {
      return;
    }

    this.bookingFlow.setSlot({
      id: slot.id,
      startTimeUtc: slot.startTimeUtc,
      endTimeUtc: slot.endTimeUtc,
    });
    void this.router.navigate(['/booking/delivery']);
  }
}
