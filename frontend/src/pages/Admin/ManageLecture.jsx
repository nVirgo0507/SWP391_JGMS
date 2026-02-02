import "./Table.css";
import Pagination from "../../components/Pagination";
import { useState } from "react";
import Modal from "../../components/coreUI/Modal";

export default function ManageLectures() {
  const [open, setOpen] = useState(false);
  const [page, setPage] = useState(1);
  return (
    <>
      <div className="page-header">
        <h1>Manage Lecturers</h1>
        <button className="btn-primary" onClick={() => setOpen(true)}>
          + Add Lecturer
        </button>
      </div>

        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>LECTURER NAME</th>
              <th>EMAIL</th>
              <th>GROUPS</th>
              <th>ACTION</th>
            </tr>
          </thead>

          <tbody>
            <tr>
              <td>L01</td>
              <td>Tuan Khai</td>
              <td>khaidtse170569@fpt.edu.vn</td>
              <td>3</td>
              <td>coding</td>
            </tr>
          </tbody>
        </table>
        <Modal
        title="Add Lecturer"
        open={open}
        onClose={() => setOpen(false)}
      >
        <input placeholder="Lecturer Name" />
        <input placeholder="Email" />
        <input placeholder="Subject" />
        <input placeholder="Phone" />
        <button className="btn-primary full">Add</button>
      </Modal>

      <Pagination
        page={page}
        totalPages={5}
        onChange={setPage}
      />
    </>
  );
}
