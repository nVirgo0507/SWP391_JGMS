import "./Modal.css";

export default function Modal({ title, open, onClose, children }) {
  if (!open) return null;

  return (
    <div className="modal-overlay">
      <div className="modal">
        <div className="modal-header">
          <h3>{title}</h3>
          <button onClick={onClose}>✕</button>
        </div>

        <div className="modal-body">
          {children}
        </div>
      </div>
    </div>
  );
}
