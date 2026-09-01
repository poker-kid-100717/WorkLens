import { Injectable } from '@angular/core';

/**
 * Extracts plain text from a PDF entirely in the browser using Mozilla's official
 * pdfjs-dist package. Keeping this client-side means the backend never needs a
 * PDF-parsing dependency (avoiding a whole class of server-side parsing
 * vulnerabilities) — only extracted plain text is ever sent to the API.
 */
@Injectable({ providedIn: 'root' })
export class PdfTextExtractor {
  async extractText(file: File): Promise<string> {
    const pdfjsLib = await import('pdfjs-dist');
    // Point the worker at the same version's prebuilt worker script from the package
    // itself (bundled by Angular's build, not a remote CDN) so extraction works offline.
    pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
      'pdfjs-dist/build/pdf.worker.mjs',
      import.meta.url
    ).toString();

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
