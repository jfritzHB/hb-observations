import type { ItemType } from './selection';

/** Mirrors the domain limit (FieldApp.Domain.Capture.ItemLocation). */
export const locationDetailMaxLength = 120;

/**
 * Everything decided before the photo. After a save, Capture Another (Slice 4) starts the next item from the
 * same context: Project (route), Area, Location Detail, Trade and Type, each individually changeable.
 */
export interface CaptureContext {
  areaId: string | null;
  /** Free-text location such as "Unit 214" or "North wall". Never creates Area master data. */
  locationDetail: string;
  tradeId: string | null;
  itemType: ItemType | null;
}

export const emptyCaptureContext: CaptureContext = {
  areaId: null,
  locationDetail: '',
  tradeId: null,
  itemType: null,
};

/** A location is a structured Area, a Location Detail, or both. */
export function hasLocation(context: CaptureContext): boolean {
  return context.areaId !== null || context.locationDetail.trim() !== '';
}

export function isReadyForPhoto(context: CaptureContext): boolean {
  return hasLocation(context) && context.tradeId !== null && context.itemType !== null;
}

/** Router state accepted by the New Item route to start from a retained context (future Capture Another). */
export interface NewItemRouteState {
  captureContext?: Partial<CaptureContext>;
}

export function initialCaptureContext(state: unknown): CaptureContext {
  const retained = (state as NewItemRouteState | null)?.captureContext;
  return {
    ...emptyCaptureContext,
    ...retained,
    locationDetail: (retained?.locationDetail ?? '').slice(0, locationDetailMaxLength),
  };
}
