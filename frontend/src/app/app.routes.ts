import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'feed', pathMatch: 'full' },
  {
    path: 'feed',
    loadComponent: () => import('./features/feed/feed.component').then((m) => m.FeedComponent)
  },
  {
    path: 'tracker',
    loadComponent: () => import('./features/tracker/tracker.component').then((m) => m.TrackerComponent)
  },
  {
    path: 'communications',
    loadComponent: () =>
      import('./features/communications/communications.component').then((m) => m.CommunicationsComponent)
  },
  {
    path: 'analytics',
    loadComponent: () => import('./features/analytics/analytics.component').then((m) => m.AnalyticsComponent)
  },
  {
    path: 'search-profiles',
    loadComponent: () =>
      import('./features/search-profiles/search-profiles.component').then((m) => m.SearchProfilesComponent)
  },
  {
    path: 'resume',
    loadComponent: () => import('./features/resume/resume.component').then((m) => m.ResumeComponent)
  },
  { path: '**', redirectTo: 'feed' }
];
