import "./Table.css";
import Pagination from "../../components/Pagination";
import { useState } from "react";
import Modal from "../../components/coreUI/Modal";

export default function ManageGroups() {
  const [open, setOpen] = useState(false);
  const [page, setPage] = useState(1);
  return (
    <>
    <div className="page-header">
        <h1>Manage Student Groups</h1>
        <button className="btn-primary" onClick={() => setOpen(true)}>
          + Create New Group
        </button>
      </div>

        <table>
          <thead>
            <tr>
              <th>GROUP ID</th>
              <th>GROUP NAME</th>
              <th>LECTURER</th>
              <th>PROJECT</th>
              <th>ACTION</th>
            </tr>
          </thead>

          <tbody>
            <tr>
              <td>G01</td>
              <td>
                <strong>The Innovators</strong>
                <p>5 Members</p>
              </td>
              <td>Tuan Khai</td>
              <td>
                <span className="badge">E-commerce Platform</span>
              </td>
              <td>coding</td>
            </tr>
          </tbody>
        </table>
       <Modal
        title="Create New Group"
        open={open}
        onClose={() => setOpen(false)}
      >
        <input placeholder="Group Name" />
        <input placeholder="Project Title" />
        <input placeholder="Class" />
        <input placeholder="Email" />
        <input placeholder="Phone" />
        <select>
          <option>Select Lecturer</option>
          <option>Thay giao ba</option>
          <option>Anh do mixi</option>
          <option>Tu sena</option>
        </select>

        <button className="btn-primary full">Create</button>
      </Modal>
      <Pagination
        page={page}
        totalPages={5}
        onChange={setPage}
      />
    </>
  );
}
