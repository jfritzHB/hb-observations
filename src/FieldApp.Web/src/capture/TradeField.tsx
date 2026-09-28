import { useId, useState } from 'react';
import type { CaptureTrade } from '../api/types';
import { CheckIcon } from '../components/icons';
import { Notice } from '../components/StatusMessage';
import { Sheet } from '../components/Sheet';
import { quickTrades } from './selection';

interface TradeFieldProps {
  trades: CaptureTrade[];
  /** Recently used trades on this device, most recent first. */
  recent: CaptureTrade[];
  selected: CaptureTrade | null;
  onSelect: (trade: CaptureTrade) => void;
}

export function TradeField({ trades, recent, selected, onSelect }: TradeFieldProps) {
  const [open, setOpen] = useState(false);
  const headingId = useId();

  const chips = quickTrades(trades, recent, selected);

  return (
    <section className="capture-step" aria-labelledby={headingId}>
      <h2 id={headingId} className="capture-step__label">
        <span className="capture-step__number" aria-hidden="true">
          3
        </span>
        Trade
      </h2>

      {trades.length === 0 ? (
        <Notice>
          <p>
            No trades are set up for capture on this project yet. Ask a project manager to map trades to
            companies.
          </p>
        </Notice>
      ) : (
        <>
          <div className="chip-grid" role="group" aria-labelledby={headingId}>
            {chips.map((trade) => {
              const isSelected = selected?.tradeId === trade.tradeId;
              return (
                <button
                  key={trade.tradeId}
                  type="button"
                  className="chip"
                  aria-pressed={isSelected}
                  onClick={() => {
                    onSelect(trade);
                  }}
                >
                  {isSelected ? <CheckIcon size={20} /> : null}
                  {trade.name}
                </button>
              );
            })}
            <button
              type="button"
              className="chip chip--outline"
              aria-haspopup="dialog"
              onClick={() => {
                setOpen(true);
              }}
            >
              View all trades ({trades.length})
            </button>
          </div>

          <p className="responsible" aria-live="polite">
            {selected ? (
              <>
                <span className="responsible__label">Responsible company</span>
                <span className="responsible__value">{selected.responsibleCompany.name}</span>
              </>
            ) : (
              <span className="field-hint">The responsible company is filled in from the trade.</span>
            )}
          </p>
        </>
      )}

      <Sheet
        open={open}
        title="Choose trade"
        onClose={() => {
          setOpen(false);
        }}
      >
        <ul className="option-list">
          {trades.map((trade) => (
            <li key={trade.tradeId}>
              <button
                type="button"
                className="option"
                aria-pressed={selected?.tradeId === trade.tradeId}
                onClick={() => {
                  onSelect(trade);
                  setOpen(false);
                }}
              >
                <span className="option__text">
                  <span className="option__primary">{trade.name}</span>
                  <span className="option__secondary">Responsible: {trade.responsibleCompany.name}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      </Sheet>
    </section>
  );
}
