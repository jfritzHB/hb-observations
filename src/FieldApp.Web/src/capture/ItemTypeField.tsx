import { useId } from 'react';
import { itemTypeLabels, type ItemType } from './selection';

interface ItemTypeFieldProps {
  value: ItemType | null;
  onChange: (value: ItemType) => void;
}

/** Two large, equally prominent choices. Native radios give arrow-key and screen-reader behaviour for free. */
export function ItemTypeField({ value, onChange }: ItemTypeFieldProps) {
  const name = useId();

  return (
    <fieldset className="capture-field segmented">
      <legend className="visually-hidden">Item type</legend>
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
