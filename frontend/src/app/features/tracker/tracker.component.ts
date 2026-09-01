import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApplicationsService } from '../../core/services/applications.service';
import { APPLICATION_STATUSES, ApplicationStatus, CreateApplicationRequest, JobApplication } from '../../core/models/models';

@Component({
  selector: 'app-tracker',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tracker.component.html',
  styleUrl: './tracker.component.scss'
})
export class TrackerComponent implements OnInit {
  readonly statuses = APPLICATION_STATUSES;
  applications: JobApplication[] = [];
  loading = true;

  selectedApp: JobApplication | null = null;
  editNotes = '';
  editFollowUpAt = '';
  editContactName = '';
  editContactEmail = '';

  showManualForm = false;
  manualForm: CreateApplicationRequest = { title: '', company: '', location: '', url: '', notes: '' };
  manualSaving = false;
  manualError: string | null = null;

  constructor(private applicationsService: ApplicationsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.applicationsService.getAll().subscribe({
      next: (apps) => {
        this.applications = apps;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }

  byStatus(status: ApplicationStatus): JobApplication[] {
    return this.applications.filter((a) => a.status === status);
  }

  openDetail(app: JobApplication): void {
    this.selectedApp = app;
    this.editNotes = app.notes ?? '';
    this.editFollowUpAt = app.followUpAt ? app.followUpAt.substring(0, 16) : '';
    this.editContactName = app.contactName ?? '';
    this.editContactEmail = app.contactEmail ?? '';
  }

  closeDetail(): void {
    this.selectedApp = null;
  }

  changeStatus(app: JobApplication, status: ApplicationStatus): void {
    this.applicationsService.update(app.id, { status }).subscribe({
      next: (updated) => this.replaceInList(updated)
    });
  }

  saveDetail(): void {
    if (!this.selectedApp) return;
    const followUpAt = this.editFollowUpAt ? new Date(this.editFollowUpAt).toISOString() : null;

    this.applicationsService
      .update(this.selectedApp.id, {
        notes: this.editNotes,
        followUpAt,
        contactName: this.editContactName,
        contactEmail: this.editContactEmail
      })
      .subscribe({
        next: (updated) => {
          this.replaceInList(updated);
          this.selectedApp = updated;
        }
      });
  }

  dismissFollowUp(app: JobApplication, event: Event): void {
    event.stopPropagation();
    this.applicationsService.update(app.id, { followUpDismissed: true }).subscribe({
      next: (updated) => this.replaceInList(updated)
    });
  }

  deleteApplication(app: JobApplication): void {
    if (!confirm(`Remove "${app.title}" at ${app.company} from your tracker?`)) return;
    this.applicationsService.delete(app.id).subscribe({
      next: () => {
        this.applications = this.applications.filter((a) => a.id !== app.id);
        if (this.selectedApp?.id === app.id) this.selectedApp = null;
      }
    });
  }

  private replaceInList(updated: JobApplication): void {
    this.applications = this.applications.map((a) => (a.id === updated.id ? updated : a));
  }

  // "Save from URL" — the practical answer for LinkedIn/Indeed jobs, which have no
  // public feed API to auto-ingest. Paste the link and details, and it tracks exactly
  // like anything saved from the live feed.
  openManualForm(): void {
    this.showManualForm = true;
    this.manualForm = { title: '', company: '', location: '', url: '', notes: '' };
    this.manualError = null;
  }

  closeManualForm(): void {
    this.showManualForm = false;
  }

  submitManualForm(): void {
    if (!this.manualForm.title || !this.manualForm.company) {
      this.manualError = 'Title and company are required.';
      return;
    }
    this.manualSaving = true;
    this.applicationsService.create(this.manualForm).subscribe({
      next: (app) => {
        this.applications = [app, ...this.applications];
        this.manualSaving = false;
        this.showManualForm = false;
      },
      error: (err) => {
        this.manualSaving = false;
        this.manualError = err?.error ?? 'Could not save this application.';
      }
    });
  }
}
