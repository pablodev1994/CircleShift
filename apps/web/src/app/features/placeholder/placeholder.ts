import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Shell page for features that are planned but not built yet. Text comes from route data. */
@Component({
  selector: 'cs-placeholder',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="page-title">
      <h1 id="page-title">{{ heading() }}</h1>
      <p>{{ summary() }}</p>
      <p class="notice" role="note">This area is not available yet.</p>
    </section>
  `,
})
export class Placeholder {
  readonly heading = input.required<string>();
  readonly summary = input.required<string>();
}
