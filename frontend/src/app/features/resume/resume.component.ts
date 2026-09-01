import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ResumeService } from '../../core/services/resume.service';
import { PdfTextExtractor } from '../../core/services/pdf-text-extractor';
import { Resume } from '../../core/models/models';

@Component({
  selector: 'app-resume',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './resume.component.html',
  styleUrl: './resume.component.scss'
})
export class ResumeComponent implements OnInit {
  resumes: Resume[] = [];
  loading = true;

  name = '';
  pastedText = '';
  extracting = false;
  uploading = false;
  error: string | null = null;
  success: string | null = null;

  constructor(
    private resumeService: ResumeService,
    private pdfExtractor: PdfTextExtractor
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.resumeService.getAll().subscribe({
      next: (resumes) => {
        this.resumes = resumes;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }

  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.error = null;
    this.extracting = true;

    try {
      if (file.type === 'application/pdf' || file.name.toLowerCase().endsWith('.pdf')) {
        this.pastedText = await this.pdfExtractor.extractText(file);
      } else {
        this.pastedText = await file.text();
      }
      if (!this.name) this.name = file.name.replace(/\.(pdf|txt)$/i, '');
    } catch (err) {
      this.error = 'Could not read that file. Try pasting the resume text directly instead.';
    } finally {
      this.extracting = false;
      input.value = '';
    }
  }

  submit(): void {
    if (!this.pastedText.trim()) {
      this.error = 'Add resume text first — upload a PDF/text file above or paste it directly.';
      return;
    }

    this.uploading = true;
    this.error = null;
    this.resumeService.upload({ name: this.name || 'Resume', rawText: this.pastedText }).subscribe({
      next: () => {
        this.uploading = false;
        this.success = 'Resume saved. It will now be used for job match scoring on the Feed.';
        this.pastedText = '';
        this.name = '';
        this.load();
      },
      error: () => {
        this.uploading = false;
        this.error = 'Could not save the resume.';
      }
    });
  }
}
