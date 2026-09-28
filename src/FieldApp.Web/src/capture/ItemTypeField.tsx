import { useId } from 'react';
import { itemTypeLabels, type ItemType } from './selection';

interface ItemTypeFieldProps {
  value: ItemType | null;
  onChange: (value: ItemType) => void;
}

/** Two large segmented choices. Native radios give arrow-key and screen-reader behaviour for free. */
export function ItemTypeField({ value, onChange }: ItemTypeFieldProps) {
  const name = useId();

  return (
    <fieldset className="capture-step segmented">
      <legend className="capture-step__label">
        <span className="capture-step__number" aria-hidden="true">
          4
        </span>
        Type
      </legend>
      <div className="segmented__options">
        {(Object.keys(itemTypeLabels) as ItemType[]).map((type) => (
          <label key={type} className="segmented__option">
            <input
              type="radio"
              name={name}
              value={type}
              checked={value === type}
              onChange={() => {
                onChange(type);
              }}
            />
            <span>{itemTypeLabels[type]}</span>
          </label>
        ))}
      </div>
    </fieldset>
  );
}
