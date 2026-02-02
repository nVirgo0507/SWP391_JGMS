import "./Pagination.css";

export default function Pagination({ page, totalPages, onChange }) {
  return (
    <div className="pagination">
      <button
        disabled={page === 1}
        onClick={() => onChange(page - 1)}
      >
        ‹
      </button>

      {[...Array(totalPages)].map((_, i) => (
        <button
          key={i}
          className={page === i + 1 ? "active" : ""}
          onClick={() => onChange(i + 1)}
        >
          {i + 1}
        </button>
      ))}

      <button
        disabled={page === totalPages}
        onClick={() => onChange(page + 1)}
      >
        ›
      </button>
    </div>
  );
}
