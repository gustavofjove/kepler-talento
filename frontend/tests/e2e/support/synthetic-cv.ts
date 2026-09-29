/**
 * A minimal, valid PDF about a fictitious person, built in memory for the KTL-32 journey. No
 * fixture file is committed and no real personal data is involved.
 */
export interface SyntheticCvLine {
  text: string;
  size: number;
}

export function syntheticCvPdf(lines: SyntheticCvLine[]): Buffer {
  const escape = (text: string) => text.replace(/[\\()]/g, (character) => `\\${character}`);
  let y = 780;
  const content = lines
    .map((line) => {
      const operation = `BT /F1 ${line.size} Tf 50 ${y} Td (${escape(line.text)}) Tj ET`;
      y -= line.size + 8;
      return operation;
    })
    .join('\n');

  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>',
    `<< /Length ${Buffer.byteLength(content, 'latin1')} >>\nstream\n${content}\nendstream`,
  ];

  let pdf = '%PDF-1.4\n';
  const offsets: number[] = [];
  objects.forEach((body, index) => {
    offsets.push(Buffer.byteLength(pdf, 'latin1'));
    pdf += `${index + 1} 0 obj\n${body}\nendobj\n`;
  });
  const xref = Buffer.byteLength(pdf, 'latin1');
  pdf += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  pdf += offsets.map((offset) => `${String(offset).padStart(10, '0')} 00000 n \n`).join('');
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return Buffer.from(pdf, 'latin1');
}
