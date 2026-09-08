// Excel's formula bar: the address of the active cell and its value, read-only —
// column A shows the composed document number exactly as the workbook's
// CONCATENATE does.
export function FormulaBar({ address, value }: { address: string; value: string | null }) {
  return (
    <div className="flex items-stretch border-b border-border bg-background text-xs">
      <div
        aria-label="Name box"
        className="w-24 shrink-0 border-r border-border px-2 py-1.5 font-mono tabular-nums"
      >
        {address}
      </div>
      <output
        aria-label="Cell value"
        className="min-w-0 flex-1 truncate px-2 py-1.5 font-[Calibri,Segoe_UI,sans-serif]"
      >
        {value ?? ''}
      </output>
    </div>
  )
}
