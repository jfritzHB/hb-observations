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

/** Large chips for recent/common trades plus More. The Responsible Company is derived, never chosen. */
export function TradeField({ trades, recent, selected, onSelect }: TradeFieldProps) {
  const [open, setOpen] = useState(false);
  const labelId = useId();

  const chips = quickTrades(trades, recent, selected);

  return (
    <div className="capture-field">
      <div className="capture-field__label-row">
        <span id={labelId} className="capture-field__label">
          Trade
        </span>
      </div>

      {trades.length === 0 ? (
        <Notice>
          <p>
            No trades are set up for capture on this project yet. Ask a project manager to map trades to
            companies.
          </p>
        </Notice>
      ) : (
        <>
          <div className="trade-grid" role="group" aria-labelledby={labelId}>
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
                  {isSelected ? <CheckIcon size={18} /> : null}
                  {trade.name}
                </button>
              );
            })}
            {trades.length > chips.length ? (
              <button
                type="button"
                className="chip chip--outline"
                aria-haspopup="dialog"
                aria-label={`More trades (${String(trades.length)} total)`}
                onClick={() => {
                  setOpen(true);
                }}
              >
                More
              </button>
            ) : null}
          </div>

          {selected ? (
            <p className="responsible" aria-live="polite">
              <span className="responsible__label">Responsible:</span>{' '}
              <span className="responsible__value">{selected.responsibleCompany.name}</span>
            </p>
          ) : null}
        </>
      )}

      <Sheet
        open={open}
        title="All trades"
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
    </div>
  );
}
