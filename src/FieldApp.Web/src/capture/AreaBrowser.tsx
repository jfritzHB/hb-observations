import type { Area } from '../api/types';
import { Sheet } from '../components/Sheet';

interface AreaBrowserProps {
  open: boolean;
  areas: Area[];
  selectedAreaId: string | null;
  onChoose: (area: Area) => void;
  onClose: () => void;
}

/** The full structured Area hierarchy, for choosing a location without typing. */
export function AreaBrowser({ open, areas, selectedAreaId, onChoose, onClose }: AreaBrowserProps) {
  return (
    <Sheet open={open} title="Browse areas" onClose={onClose}>
      <ul className="option-list">
        {areas.map((area) => {
          const separator = area.path.lastIndexOf(' / ');
          const parentPath = separator >= 0 ? area.path.slice(0, separator + 3) : '';
          return (
            <li key={area.id}>
              <button
                type="button"
                className="option"
                style={{ paddingInlineStart: `${String(16 + area.depth * 20)}px` }}
                aria-label={area.path}
                aria-pressed={selectedAreaId === area.id}
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
    </Sheet>
  );
}
