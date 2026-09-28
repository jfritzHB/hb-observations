import type { DevPersona } from '../api/types';
import { CheckIcon } from '../components/icons';

interface PersonaListProps {
  personas: DevPersona[];
  current: string | null;
  onChoose: (key: string) => void;
}

export function PersonaList({ personas, current, onChoose }: PersonaListProps) {
  return (
    <ul className="choice-list" aria-label="Development personas">
      {personas.map((persona) => {
        const selected = persona.key === current;
        return (
          <li key={persona.key}>
            <button
              type="button"
              className="choice"
              aria-pressed={selected}
              onClick={() => {
                onChoose(persona.key);
              }}
            >
              <span className="choice__text">
                <span className="choice__title">
                  {persona.displayName} <span className="badge">{persona.roleLabel}</span>
                </span>
                <span className="choice__detail">{persona.description}</span>
              </span>
              {selected ? <CheckIcon /> : null}
            </button>
          </li>
        );
      })}
    </ul>
  );
}
