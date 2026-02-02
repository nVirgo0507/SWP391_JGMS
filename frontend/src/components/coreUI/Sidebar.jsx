import { NavLink } from "react-router-dom";
import "./Sidebar.css";

export default function Sidebar() {
  return (
    <aside className="sidebar">
      <h2 className="logo">Admin</h2>

      <nav className="menu">
        <NavLink to="/admin" end>Dashboard</NavLink>
        <NavLink to="/admin/groups">Manage Groups</NavLink>
        <NavLink to="/admin/lectures">Manage Lectures</NavLink>
      </nav>
    </aside>
  );
}