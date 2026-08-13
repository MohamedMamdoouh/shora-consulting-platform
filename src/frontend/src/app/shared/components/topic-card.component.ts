import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import type { Topic } from '../../public/shared/topic.constants';

@Component({
  selector: 'app-topic-card',
  imports: [RouterLink],
  host: {
    class: 'topic-card-host',
    '[class]': '"topic-card-host--" + topic().accent',
    '[attr.id]': '"topic-" + topic().id',
  },
  template: `
    <article class="topic-card">
      <span
        class="topic-card__icon"
        [class]="'topic-card__icon--' + topic().accent"
        aria-hidden="true"
      >
        @switch (topic().id) {
            @case ('communication') {
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none" aria-hidden="true">
                <path
                  d="M4 6.5A2.5 2.5 0 016.5 4H12a2.5 2.5 0 012.5 2.5V12A2.5 2.5 0 0112 14.5H9l-2.5 2.5V14.5H6.5A2.5 2.5 0 014 12V6.5z"
                  fill="currentColor"
                  opacity="0.35"
                />
                <path
                  d="M9 8A2.5 2.5 0 0111.5 5.5H17a2.5 2.5 0 012.5 2.5V13A2.5 2.5 0 0117 15.5h-3L12 18v-2.5h-0.5A2.5 2.5 0 019 13V8z"
                  fill="currentColor"
                />
                <path
                  d="M13 9.5h3M13 11.5h2"
                  stroke="currentColor"
                  stroke-width="1.5"
                  stroke-linecap="round"
                />
              </svg>
            }
            @case ('trust') {
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none" aria-hidden="true">
                <path
                  d="M12 20.5S6 16 6 10.2C6 7.6 8 5.5 10.6 5.5c1.4 0 2.7.7 3.4 1.7.7-1 2-1.7 3.4-1.7 2.6 0 4.6 2.1 4.6 4.7 0 5.8-6 10.8-6 10.8z"
                  stroke="currentColor"
                  stroke-width="1.75"
                  stroke-linejoin="round"
                />
                <path
                  d="M12 7.5v6M10 10.5l2 2 2-2"
                  stroke="currentColor"
                  stroke-width="1.75"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
              </svg>
            }
            @case ('dating-confidence') {
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none" aria-hidden="true">
                <path
                  d="M12 19.5S7 15.5 7 10.5c0-2.2 1.8-3.8 3.7-3.8 1.1 0 2.1.5 2.8 1.3.7-.8 1.7-1.3 2.8-1.3 1.9 0 3.7 1.6 3.7 3.8 0 5-5 9-5 9z"
                  stroke="currentColor"
                  stroke-width="1.75"
                  stroke-linejoin="round"
                />
                <circle cx="17.5" cy="7" r="2.25" stroke="currentColor" stroke-width="1.5" />
                <path
                  d="M17.5 6v1.6M17.5 9.1v.01"
                  stroke="currentColor"
                  stroke-width="1.5"
                  stroke-linecap="round"
                />
              </svg>
            }
            @case ('long-distance') {
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none" aria-hidden="true">
                <path
                  d="M7 19V11l2.5-1.5L12 11v8"
                  stroke="currentColor"
                  stroke-width="1.75"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
                <path
                  d="M12 19V11l2.5-1.5L17 11v8"
                  stroke="currentColor"
                  stroke-width="1.75"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
                <circle cx="7" cy="19" r="1.75" fill="currentColor" />
                <circle cx="17" cy="19" r="1.75" fill="currentColor" />
                <path
                  d="M7 19c2.5-4.5 7.5-4.5 10 0"
                  stroke="currentColor"
                  stroke-width="1.75"
                  stroke-linecap="round"
                  stroke-dasharray="2.5 2.5"
                />
                <path
                  d="M12 13.5v-1.5"
                  stroke="currentColor"
                  stroke-width="1.5"
                  stroke-linecap="round"
                />
              </svg>
            }
          }
      </span>

      <div class="topic-card__body">
        <h3 class="topic-card__title">{{ topic().title }}</h3>
        <p class="topic-card__description">{{ description() }}</p>

        @if (showDiscoverLink()) {
          <a
            class="topic-card__link"
            [class]="'topic-card__link--' + topic().accent"
            routerLink="/booking/start"
          >
            {{ copy.cta.discoverMore }}
            <span aria-hidden="true">←</span>
          </a>
        }
      </div>
    </article>
  `,
  styles: `
    .topic-card {
      display: grid;
      grid-template-columns: auto 1fr;
      column-gap: var(--space-sm);
      align-items: start;
      height: 100%;
      padding: var(--space-md);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-card);
      transition:
        box-shadow var(--transition-base),
        transform var(--transition-base);
    }

    .topic-card:hover {
      box-shadow: var(--shadow-card-hover);
      transform: translateY(-2px);
    }

    .topic-card__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      grid-column: 1;
      grid-row: 1;
      width: 2.5rem;
      height: 2.5rem;
      border-radius: var(--radius-full);
    }

    .topic-card__body {
      grid-column: 2;
      grid-row: 1;
      display: flex;
      flex-direction: column;
      gap: var(--space-xs);
      min-width: 0;
    }

    .topic-card__icon--purple {
      background: var(--color-topic-purple-bg);
      color: var(--color-topic-purple);
    }

    .topic-card__icon--green {
      background: var(--color-topic-green-bg);
      color: var(--color-topic-green);
    }

    .topic-card__icon--orange {
      background: var(--color-topic-orange-bg);
      color: var(--color-topic-orange);
    }

    .topic-card__icon--pink {
      background: var(--color-topic-pink-bg);
      color: var(--color-topic-pink);
    }

    .topic-card__icon--sky {
      background: var(--color-topic-sky-bg);
      color: var(--color-topic-sky);
    }

    .topic-card__title {
      margin: 0;
      font-size: var(--font-size-base);
      font-weight: 700;
      line-height: 1.35;
    }

    .topic-card__description {
      margin: 0;
      color: var(--color-text-muted);
      font-size: var(--font-size-sm);
      line-height: 1.5;
    }

    .topic-card__link {
      margin-top: var(--space-sm);
      display: inline-flex;
      align-items: center;
      gap: var(--space-xs);
      width: fit-content;
      font-size: var(--font-size-sm);
      font-weight: 600;
      text-decoration: none;
      transition: gap var(--transition-fast);
    }

    .topic-card__link:hover {
      gap: var(--space-sm);
    }

    .topic-card__link--purple {
      color: var(--color-topic-purple);
    }

    .topic-card__link--green {
      color: var(--color-topic-green);
    }

    .topic-card__link--orange {
      color: var(--color-topic-orange);
    }

    .topic-card__link--pink {
      color: var(--color-topic-pink);
    }

    .topic-card__link--sky {
      color: var(--color-topic-sky);
    }
  `,
})
export class TopicCardComponent {
  readonly topic = input.required<Topic>();
  readonly description = input.required<string>();
  readonly showDiscoverLink = input(false);

  protected readonly copy = APP_COPY;
}
