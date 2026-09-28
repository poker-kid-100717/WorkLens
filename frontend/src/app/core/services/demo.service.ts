import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { resolveApiBase } from './api-config';

/** Whether the API is running as the read-only public demo (see Demo:Enabled on the API). */
@Injectable({ providedIn: 'root' })
export class DemoService {
  private readonly http = inject(HttpClient);

  readonly enabled$: Observable<boolean> = this.http
    .get<{ enabled: boolean }>(`${resolveApiBase()}/demo`)
    .pipe(
      map((r) => r.enabled === true),
      catchError(() => of(false)),
      shareReplay(1)
    );
}
