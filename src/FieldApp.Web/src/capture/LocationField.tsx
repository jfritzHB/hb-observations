import { useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import type { Area } from '../api/types';
import { CloseIcon, LocationIcon } from '../components/icons';
import { AreaBrowser } from './AreaBrowser';
import { locationDetailMaxLength } from './captureContext';
import { locationAccessibleName, locationLabel, matchesSearch, type LocationChoice } from './selection';

interface LocationFieldProps {
  areas: Area[];
  area: Area | null;
  locationDetail: string;
  /** Recently used locations on this device, most recent first. */
  recent: LocationChoice[];
  onChange: (choice: LocationChoice) => void;
  /** A location was deliberately chosen or finished (used to remember recent locations). */
  onCommit: (choice: LocationChoice) => void;
}

const maxSuggestions = 6;

/**
 * "Where?": one field to search structured Areas or type a free-text Location Detail (or both). Typed text is the
 * Location Detail unless the user picks a matching Area; it never creates Area master data.
 * Implements the ARIA 1.2 combobox pattern with a listbox popup.
 */
export function LocationField({
  areas,
  area,
  locationDetail,
  recent,
  onChange,
  onCommit,
}: LocationFieldProps) {
  const inputId = useId();
  const listId = useId();
  const optionId = useId();
  const [focused, setFocused] = useState(false);
  const [dismissed, setDismissed] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const [browsing, setBrowsing] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const recentAreaIds = useMemo(
    () => new Set(recent.flatMap((choice) => (choice.area ? [choice.area.id] : []))),
    [recent],
  );

  const suggestions = useMemo(() => {
    const query = locationDetail.trim();
    if (!query) {
      return [];
    }

    return areas
      .filter((candidate) => candidate.id !== area?.id && matchesSearch(candidate, query))
      .sort((a, b) => Number(recentAreaIds.has(b.id)) - Number(recentAreaIds.has(a.id)))
      .slice(0, maxSuggestions);
  }, [areas, area, locationDetail, recentAreaIds]);

  const expanded = focused && !dismissed && suggestions.length > 0;

  const chooseArea = (chosen: Area) => {
    // The typed text was a search for this Area, so it is not kept as detail.
    const choice = { area: chosen, locationDetail: '' };
    onChange(choice);
    onCommit(choice);
    setActiveIndex(-1);
    setDismissed(true);
  };

  const commitDetail = () => {
    if (locationDetail.trim()) {
      onCommit({ area, locationDetail });
    }
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        setDismissed(false);
        setActiveIndex((index) => (suggestions.length === 0 ? -1 : (index + 1) % suggestions.length));
        break;
      case 'ArrowUp':
        event.preventDefault();
        setDismissed(false);
        setActiveIndex((index) =>
          suggestions.length === 0 ? -1 : (index - 1 + suggestions.length) % suggestions.length,
        );
        break;
      case 'Enter': {
        const active = expanded ? suggestions[activeIndex] : undefined;
        if (active) {
          event.preventDefault();
          chooseArea(active);
        } else {
          setDismissed(true);
          commitDetail();
        }
        break;
      }
      case 'Escape':
        if (expanded) {
          event.preventDefault();
          setDismissed(true);
          setActiveIndex(-1);
        }
        break;
    }
  };

  const visibleRecent = recent.filter(
    (choice) => !(choice.area?.id === area?.id && choice.locationDetail.trim() === locationDetail.trim()),
  );

  return (
    <div className="capture-field">
      <div className="capture-field__label-row">
        <label className="capture-field__label" htmlFor={inputId}>
          Where?
        </label>
        <button
          type="button"
          className="text-button text-button--small"
          aria-haspopup="dialog"
          onClick={() => {
            setBrowsing(true);
          }}
        >
          Browse areas
        </button>
      </div>

      {area ? (
        <div className="location-area">
          <LocationIcon size={20} />
          <span className="location-area__path">{area.path}</span>
          <button
            type="button"
            className="icon-button icon-button--plain"
            aria-label={`Remove area ${area.path}`}
            onClick={() => {
              onChange({ area: null, locationDetail });
              inputRef.current?.focus();
            }}
          >
            <CloseIcon size={20} />
          </button>
        </div>
      ) : null}

      <div className="combobox">
        <input
          ref={inputRef}
          id={inputId}
          className="combobox__input"
          type="text"
          role="combobox"
          aria-expanded={expanded}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={
            expanded && activeIndex >= 0 ? `${optionId}-${String(activeIndex)}` : undefined
          }
          aria-describedby={area ? undefined : `${inputId}-hint`}
          value={locationDetail}
          maxLength={locationDetailMaxLength}
          placeholder={area ? 'Add location detail (optional)' : 'Search area or type unit, room…'}
          autoComplete="off"
          enterKeyHint="done"
          onFocus={() => {
            setFocused(true);
          }}
          onBlur={() => {
            setFocused(false);
            setActiveIndex(-1);
            commitDetail();
          }}
          onChange={(event) => {
            onChange({ area, locationDetail: event.target.value });
            setDismissed(false);
            setActiveIndex(-1);
          }}
          onKeyDown={handleKeyDown}
        />
        {area ? null : (
          <span id={`${inputId}-hint`} className="visually-hidden">
            Type to search project areas, or enter any location such as a unit or room.
          </span>
        )}

        <div className="combobox__popup" hidden={!expanded}>
          {area ? (
            <p className="combobox__heading" aria-hidden="true">
              Replace area with
            </p>
          ) : null}
          <ul
            id={listId}
            role="listbox"
            aria-label={area ? 'Replace area' : 'Matching areas'}
            className="combobox__list"
          >
            {suggestions.map((suggestion, index) => (
              <li
                key={suggestion.id}
                id={`${optionId}-${String(index)}`}
                role="option"
                aria-selected={index === activeIndex}
                className="combobox__option"
                onMouseDown={(event) => {
                  // Keep focus in the input so the list does not close before the click lands.
                  event.preventDefault();
                }}
                onClick={() => {
                  chooseArea(suggestion);
                }}
              >
                <LocationIcon size={18} />
                <span>{suggestion.path}</span>
              </li>
            ))}
          </ul>
        </div>
      </div>

      <p className="visually-hidden" role="status" aria-live="polite">
        {!expanded
          ? ''
          : area
            ? `${String(suggestions.length)} ${suggestions.length === 1 ? 'area' : 'areas'} found. Choosing one replaces ${area.name}. Keep typing to add location detail.`
            : `${String(suggestions.length)} matching ${suggestions.length === 1 ? 'area' : 'areas'}. Use arrow keys to choose, or keep typing to use your text as the location.`}
      </p>

      {!expanded && visibleRecent.length > 0 ? (
        <div className="chip-row" role="group" aria-label="Recent locations">
          {visibleRecent.map((choice) => (
            <button
              key={`${choice.area?.id ?? ''}|${choice.locationDetail}`}
              type="button"
              className="chip chip--small"
              aria-label={locationAccessibleName(choice)}
              onClick={() => {
                onChange(choice);
                onCommit(choice);
              }}
            >
              {locationLabel(choice)}
            </button>
          ))}
        </div>
      ) : null}

      <AreaBrowser
        open={browsing}
        areas={areas}
        selectedAreaId={area?.id ?? null}
        onChoose={(chosen) => {
          setBrowsing(false);
          const choice = { area: chosen, locationDetail };
          onChange(choice);
          onCommit(choice);
        }}
        onClose={() => {
          setBrowsing(false);
        }}
      />
    </div>
  );
}
