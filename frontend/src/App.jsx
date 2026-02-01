import { BrowserRouter, Routes, Route } from "react-router-dom";
import AdminLayout from "./layouts/AdminLayout";
import Dashboard from "./pages/Admin/Dashboard";
import ManageGroups from "./pages/Admin/ManageGroups";
import ManageLectures from "./pages/Admin/ManageLecture";

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/admin" element={<AdminLayout />}>
          <Route index element={<Dashboard />} />
          <Route path="groups" element={<ManageGroups />} />
          <Route path="lectures" element={<ManageLectures />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;