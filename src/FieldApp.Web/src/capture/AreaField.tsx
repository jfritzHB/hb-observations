import { useId, useMemo, useState } from 'react';
import type { Area } from '../api/types';
import { LocationIcon } from '../components/icons';
import { Sheet } from '../components/Sheet';
import { matchesSearch } from './selection';

interface AreaFieldProps {
  areas: Area[];
  recent: Area[];
  selected: Area | null;
  onSelect: (area: Area) => void;
}

export function AreaField({ areas, recent, selected, onSelect }: AreaFieldProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const headingId = useId();
  const searchId = useId();

  const matches = useMemo(
    () => (query.trim() ? areas.filter((area) => matchesSearch(area, query)) : areas),
    [areas, query],
  );

  const choose = (area: Area) => {
    onSelect(area);
    setOpen(false);
    setQuery('');
  };

  return (
    <section className="capture-step" aria-labelledby={headingId}>
      <h2 id={headingId} className="capture-step__label">
        <span className="capture-step__number" aria-hidden="true">
          2
        </span>
        Area
      </h2>

      <button
        type="button"
        className={`select-button${selected ? ' select-button--filled' : ''}`}
        aria-haspopup="dialog"
        onClick={() => {
          setOpen(true);
        }}
      >
        <LocationIcon />
        <span className="select-button__value">
          {selected ? (
            <>
              <span className="visually-hidden">Area:</span> {selected.path}
            </>
          ) : (
            'Choose area'
          )}
        </span>
        {selected ? <span className="select-button__action">Change</span> : null}
      </button>

      {recent.length > 0 ? (
        <div className="chip-row" role="group" aria-label="Recent areas">
          {recent.map((area) => (
            <button
              key={area.id}
              type="button"
              className="chip chip--small"
              aria-pressed={selected?.id === area.id}
              onClick={() => {
                onSelect(area);
              }}
            >
              {area.path}
            </button>
          ))}
        </div>
      ) : null}

      <Sheet
        open={open}
        title="Choose area"
        onClose={() => {
          setOpen(false);
          setQuery('');
        }}
      >
        <label className="field-label" htmlFor={searchId}>
          Search areas <span className="field-hint">(optional)</span>
        </label>
        <input
          id={searchId}
          className="search-input"
          type="search"
          value={query}
          placeholder="e.g. Level 2 office"
          autoComplete="off"
          enterKeyHint="search"
          onChange={(event) => {
            setQuery(event.target.value);
          }}
        />

        {query.trim() ? (
          <>
            <p className="field-hint" role="status">
              {matches.length === 1 ? '1 area matches' : `${String(matches.length)} areas match`}
            </p>
            <AreaOptions areas={matches} selected={selected} onChoose={choose} flat />
          </>
        ) : (
          <>
            {recent.length > 0 ? (
              <>
                <h3 className="list-heading">Recent</h3>
                <AreaOptions areas={recent} selected={selected} onChoose={choose} flat />
              </>
            ) : null}
            <h3 className="list-heading">All areas</h3>
            <AreaOptions areas={areas} selected={selected} onChoose={choose} flat={false} />
          </>
        )}
      </Sheet>
    </section>
  );
}

function AreaOptions({
  areas,
  selected,
  onChoose,
  flat,
}: {
  areas: Area[];
  selected: Area | null;
  onChoose: (area: Area) => void;
  flat: boolean;
}) {
  if (areas.length === 0) {
    return <p className="field-hint">No areas match. Try fewer words.</p>;
  }

  return (
    <ul className="option-list">
      {areas.map((area) => {
        const separator = area.path.lastIndexOf(' / ');
        const parentPath = separator >= 0 ? area.path.slice(0, separator + 3) : '';
        return (
          <li key={area.id}>
            <button
              type="button"
              className="option"
              style={flat ? undefined : { paddingInlineStart: `${String(16 + area.depth * 20)}px` }}
              aria-label={area.path}
              aria-pressed={selected?.id === area.id}
              onClick={() => {
                onChoose(area);
              }}
            >
              <span className="option__text">
                {parentPath ? <span className="option__secondary">{parentPath}</span> : null}
                <span className="option__primary">{area.name}</span>
              </span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}
