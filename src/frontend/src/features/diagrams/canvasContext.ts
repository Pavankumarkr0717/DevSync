import { createContext, useContext } from 'react';

/** Commit callbacks the custom nodes need (React Flow renders them outside the canvas component's props). */
export interface CanvasActions {
  editable: boolean;
  resized: (id: string, box: { x: number; y: number; width: number; height: number }) => void;
}

export const CanvasActionsContext = createContext<CanvasActions>({ editable: false, resized: () => undefined });

export const useCanvasActions = () => useContext(CanvasActionsContext);
