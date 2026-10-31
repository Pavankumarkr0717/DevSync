import type { ComponentType } from '../../shared/api/types';
import { ComponentIcon } from './ComponentIcon';
import { COMPONENT_TYPES, DRAG_TYPE } from './componentTypes';

/** The component palette (spec 004 FR-013): drag a type onto the canvas, or click to place it at the view centre. */
export function Palette({ disabled, onAdd }: { disabled: boolean; onAdd: (type: ComponentType) => void }) {
  return (
    <section className="floating-panel palette" aria-label="Components" data-testid="palette">
      <p className="panel-title">Components</p>
      {COMPONENT_TYPES.map((t) => (
        <button
          key={t.type}
          type="button"
          draggable={!disabled}
          disabled={disabled}
          aria-label={`Add ${t.label}`}
          title={disabled ? 'This diagram has the maximum of 500 components' : `Add ${t.label}`}
          onClick={() => onAdd(t.type)}
          onDragStart={(event) => {
            event.dataTransfer.setData(DRAG_TYPE, t.type);
            event.dataTransfer.effectAllowed = 'copy';
          }}
        >
          <span className={`component-icon pastel-${t.pastel}`}>
            <ComponentIcon type={t.type} size={18} />
          </span>
          <span className="palette-label">{t.label}</span>
        </button>
      ))}
    </section>
  );
}
