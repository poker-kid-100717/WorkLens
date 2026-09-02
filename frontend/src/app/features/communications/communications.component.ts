import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OutlookCommunication, OutlookConnectionStatus } from '../../core/models/models';
import { OutlookService } from '../../core/services/outlook.service';

@Component({
  selector: 'app-communications',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './communications.component.html',
  styleUrl: './communications.component.scss'
})
export class CommunicationsComponent implements OnInit {
  status: OutlookConnectionStatus | null = null;
  communications: OutlookCommunication[] = [];
  loading = true;
  syncing = false;
  error: string | null = null;
  filter = 'All';

  constructor(private outlook: OutlookService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = null;
    this.outlook.getStatus().subscribe({
      next: (status) => {
        this.status = status;
        if (!status.isConnected) {
          this.communications = [];
          this.loading = false;
          return;
        }
        this.outlook.getCommunications().subscribe({
          next: (items) => {
            this.communications = items;
            this.loading = false;
          },
          error: () => {
            this.error = 'Could not load Outlook communications.';
            this.loading = false;
          }
        });
      },
      error: () => {
        this.error = 'Could not read Outlook connection status.';
        this.loading = false;
      }
    });
  }

  connect(): void {
    this.outlook.connect();
  }

  sync(): void {
    this.syncing = true;
    this.error = null;
    this.outlook.sync().subscribe({
      next: () => {
        this.syncing = false;
        this.load();
      },
      error: (err) => {
        this.syncing = false;
        this.error = err?.error?.message ?? err?.error ?? 'Outlook sync failed.';
      }
    });
  }

  kinds(): string[] {
    return ['All', ...Array.from(new Set(this.communications.map(x => x.kind))).sort()];
  }

  filtered(): OutlookCommunication[] {
    return this.filter === 'All' ? this.communications : this.communications.filter(x => x.kind === this.filter);
  }

  open(message: OutlookCommunication): void {
    if (message.webLink) window.open(message.webLink, '_blank', 'noopener');
  }
}
