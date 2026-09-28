import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([]), provideHttpClient(withXhr())]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the primary navigation', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('nav.main-nav')).toBeTruthy();
  });

  describe('demo banner', () => {
    beforeEach(() => {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [AppComponent],
        providers: [provideRouter([]), provideHttpClient(withXhr()), provideHttpClientTesting()]
      });
    });

    function render(demoEnabled: boolean): HTMLElement {
      const fixture = TestBed.createComponent(AppComponent);
      fixture.detectChanges();
      const http = TestBed.inject(HttpTestingController);
      http.expectOne('/api/demo').flush({ enabled: demoEnabled });
      http.match(() => true).forEach((req) => req.flush([]));
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    it('is shown when the API runs as the public demo', () => {
      expect(render(true).querySelector('[data-testid="banner-demo"]')).toBeTruthy();
    });

    it('is hidden otherwise', () => {
      expect(render(false).querySelector('[data-testid="banner-demo"]')).toBeNull();
    });
  });
});
