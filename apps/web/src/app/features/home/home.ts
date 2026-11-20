import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

interface LifecycleStep {
  readonly title: string;
  readonly detail: string;
}

@Component({
  selector: 'cs-home',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.html',
})
export class Home {
  protected readonly steps: readonly LifecycleStep[] = [
    { title: 'Post', detail: 'Share a skill you can offer and one you want to learn.' },
    { title: 'Match', detail: 'Find a member whose skills complement yours.' },
    { title: 'Invite', detail: 'Send an invitation; they accept or decline.' },
    { title: 'Schedule', detail: 'Agree on a time, shown in each person’s own time zone.' },
    { title: 'Confirm', detail: 'Both people confirm the session happened.' },
    { title: 'Settle', detail: 'Time credits move once: earned by one, spent by the other.' },
  ];
}
