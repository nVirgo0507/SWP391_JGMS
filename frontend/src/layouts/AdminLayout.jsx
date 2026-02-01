import { Outlet } from "react-router-dom";
import Sidebar from "../components/coreUI/Sidebar";
import Topbar from "../components/coreUI/Topbar";
import "./AdminLayout.css";

export default function AdminLayout() {
  return (
    <div className="admin-layout">
      <Sidebar />

      <div className="admin-main">
        <Topbar />
        <main className="admin-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
