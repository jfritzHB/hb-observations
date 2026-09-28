import { useEffect, useId, useState } from 'react';
import { Link, useParams } from 'react-router';
import { isApiError } from '../api/client';
import type { Area, CaptureTrade, Project } from '../api/types';
import { useApiGet, type Loadable } from '../api/useApiGet';
import { CameraIcon } from '../components/icons';
import { ErrorMessage, Loading } from '../components/StatusMessage';
import { ProjectNotFound } from '../pages/ProjectNotFound';
import { useCurrentUser } from '../session/session';
import { AreaField } from './AreaField';
import { ItemTypeField } from './ItemTypeField';
import { pickRecent, readRecent, recordRecent, writeLastProjectId } from './recent';
import { itemTypeLabels, type ItemType } from './selection';
import { TradeField } from './TradeField';

/**
 * Capture selection (docs/02-mobile-ux.md): Project, Area, Trade, Type, then the camera. No typing is needed.
 * The camera is the Slice 2 boundary and is deliberately not available yet.
 */
export function NewItemPage() {
  const { projectId = '' } = useParams();
  const base = `/projects/${encodeURIComponent(projectId)}`;
  const project = useApiGet<Project>(base);
  const areas = useApiGet<Area[]>(`${base}/areas`);
  const trades = useApiGet<CaptureTrade[]>(`${base}/trades`);

  const failed = [project.result, areas.result, trades.result].find(
    (result): result is Extract<Loadable<unknown>, { state: 'error' }> => result.state === 'error',
  );

  if (failed) {
    return isApiError(failed.error, 404) ? (
      <ProjectNotFound />
    ) : (
      <main className="page">
        <h1>New item</h1>
        <ErrorMessage
          message="Project data could not be loaded. Nothing has been lost."
          onRetry={() => {
            project.reload();
            areas.reload();
            trades.reload();
          }}
        />
      </main>
    );
  }

  if (project.result.state !== 'ready' || areas.result.state !== 'ready' || trades.result.state !== 'ready') {
    return (
      <main className="page">
        <h1>New item</h1>
        <Loading label="Loading project areas and trades…" />
      </main>
    );
  }

  return (
    <CaptureSelection project={project.result.data} areas={areas.result.data} trades={trades.result.data} />
  );
}

function CaptureSelection({
  project,
  areas,
  trades,
}: {
  project: Project;
  areas: Area[];
  trades: CaptureTrade[];
}) {
  const user = useCurrentUser();
  const [areaId, setAreaId] = useState<string | null>(null);
  const [tradeId, setTradeId] = useState<string | null>(null);
  const [itemType, setItemType] = useState<ItemType | null>(null);
  const [recentAreaIds, setRecentAreaIds] = useState(() => readRecent(user.id, project.id, 'areas'));
  const [recentTradeIds, setRecentTradeIds] = useState(() => readRecent(user.id, project.id, 'trades'));
  const summaryId = useId();
  const cameraNoteId = useId();

  useEffect(() => {
    writeLastProjectId(user.id, project.id);
  }, [user.id, project.id]);

  const area = areas.find((candidate) => candidate.id === areaId) ?? null;
  const trade = trades.find((candidate) => candidate.tradeId === tradeId) ?? null;
  const ready = area !== null && trade !== null && itemType !== null;

  return (
    <main className="page capture">
      <h1>New item</h1>

      <section className="capture-step capture-step--project" aria-label="Project">
        <span className="capture-step__number" aria-hidden="true">
          1
        </span>
        <span className="capture-project">
          <span className="capture-project__name">{project.name}</span>
          <span className="card__meta">{project.number}</span>
        </span>
        <Link className="text-button" to="/projects" aria-label={`Change project (current: ${project.name})`}>
          Change
        </Link>
      </section>

      <AreaField
        areas={areas}
        recent={pickRecent(recentAreaIds, areas, (candidate) => candidate.id, 3)}
        selected={area}
        onSelect={(selected) => {
          setAreaId(selected.id);
          setRecentAreaIds(recordRecent(user.id, project.id, 'areas', selected.id));
        }}
      />

      <TradeField
        trades={trades}
        recent={pickRecent(recentTradeIds, trades, (candidate) => candidate.tradeId, 4)}
        selected={trade}
        onSelect={(selected) => {
          setTradeId(selected.tradeId);
          setRecentTradeIds(recordRecent(user.id, project.id, 'trades', selected.tradeId));
        }}
      />

      <ItemTypeField value={itemType} onChange={setItemType} />

      <section className="summary" aria-labelledby={summaryId}>
        <div className="summary__header">
          <h2 id={summaryId} className="summary__title">
            Selections
          </h2>
          {area || trade || itemType ? (
            <button
              type="button"
              className="text-button"
              onClick={() => {
                setAreaId(null);
                setTradeId(null);
                setItemType(null);
              }}
            >
              Clear all
            </button>
          ) : null}
        </div>
        <dl className="summary__list">
          <SummaryRow term="Area" value={area?.path} />
          <SummaryRow term="Trade" value={trade?.name} />
          <SummaryRow term="Responsible company" value={trade?.responsibleCompany.name} />
          <SummaryRow term="Type" value={itemType ? itemTypeLabels[itemType] : undefined} />
        </dl>
      </section>

      <div className="capture-step capture-step--camera">
        <button type="button" className="camera-button" aria-disabled="true" aria-describedby={cameraNoteId}>
          <span className="capture-step__number capture-step__number--inverse" aria-hidden="true">
            5
          </span>
          <CameraIcon size={32} />
          Take photo
        </button>
        <p id={cameraNoteId} className={`camera-note${ready ? ' camera-note--ready' : ''}`} role="status">
          {ready
            ? 'Selections complete. Photo capture is not available yet; it arrives in the next release.'
            : 'Choose an area, trade and type first.'}
        </p>
      </div>
    </main>
  );
}

function SummaryRow({ term, value }: { term: string; value: string | undefined }) {
  return (
    <div className="summary__row">
      <dt>{term}</dt>
      <dd className={value ? undefined : 'summary__missing'}>{value ?? 'Not selected'}</dd>
    </div>
  );
}
