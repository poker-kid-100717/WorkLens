import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SearchProfilesService } from '../../core/services/search-profiles.service';
import { SaveSearchProfileRequest, SearchProfile } from '../../core/models/models';

/**
 * Manages named keyword filters that drive what the backend's background refresh
 * searches for across RemoteOK, Remotive, Greenhouse, and Dice. Editing these here
 * takes effect on the *next* background refresh cycle — it doesn't call upstream
 * feeds directly from the browser.
 */
@Component({
  selector: 'app-search-profiles',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './search-profiles.component.html',
  styleUrl: './search-profiles.component.scss'
})
export class SearchProfilesComponent implements OnInit {
  profiles: SearchProfile[] = [];
  loading = true;

  form: SaveSearchProfileRequest = this.emptyForm();
  keywordsText = '';
  editingId: number | null = null;

  constructor(private service: SearchProfilesService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.service.getAll().subscribe({
      next: (profiles) => {
        this.profiles = profiles;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }

  emptyForm(): SaveSearchProfileRequest {
    return { name: '', keywords: [], remoteOnly: false, locationFilter: '', isActive: true };
  }

  edit(profile: SearchProfile): void {
    this.editingId = profile.id;
    this.form = {
      name: profile.name,
      keywords: [...profile.keywords],
      remoteOnly: profile.remoteOnly,
      locationFilter: profile.locationFilter ?? '',
      isActive: profile.isActive
    };
    this.keywordsText = profile.keywords.join(', ');
  }

  resetForm(): void {
    this.editingId = null;
    this.form = this.emptyForm();
    this.keywordsText = '';
  }

  submit(): void {
    this.form.keywords = this.keywordsText
      .split(',')
      .map((k) => k.trim())
      .filter((k) => k.length > 0);

    if (!this.form.name || this.form.keywords.length === 0) return;

    const request = this.editingId
      ? this.service.update(this.editingId, this.form)
      : this.service.create(this.form);

    request.subscribe({
      next: () => {
        this.resetForm();
        this.load();
      }
    });
  }

  toggleActive(profile: SearchProfile): void {
    this.service
      .update(profile.id, {
        name: profile.name,
        keywords: profile.keywords,
        remoteOnly: profile.remoteOnly,
        locationFilter: profile.locationFilter,
        isActive: !profile.isActive
      })
      .subscribe({ next: () => this.load() });
  }

  delete(profile: SearchProfile): void {
    if (!confirm(`Delete search profile "${profile.name}"?`)) return;
    this.service.delete(profile.id).subscribe({ next: () => this.load() });
  }
}
