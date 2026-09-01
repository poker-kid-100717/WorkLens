import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class PdfTextExtractor {
  async extractText(file: File): Promise<string> {
    const pdfjsLib = await import('pdfjs-dist');

    // Serve the worker as a real static module so nginx returns JavaScript instead of
    // falling back to index.html (which causes the strict MIME-type failure).
    pdfjsLib.GlobalWorkerOptions.workerSrc = '/assets/pdfjs/pdf.worker.min.mjs';

    const arrayBuffer = await file.arrayBuffer();
    const pdf = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;

    const pageTexts: string[] = [];
    for (let pageNum = 1; pageNum <= pdf.numPages; pageNum++) {
      const page = await pdf.getPage(pageNum);
      const content = await page.getTextContent();
      const pageText = content.items.map((item: any) => item.str ?? '').join(' ');
      pageTexts.push(pageText);
    }

    return pageTexts.join('\n\n').trim();
  }
}
