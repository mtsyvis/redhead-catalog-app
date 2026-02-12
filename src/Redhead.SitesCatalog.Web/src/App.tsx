import { useState, useEffect } from "react";
import "./App.css";

interface HealthResponse {
  status: string;
  message: string;
}

function App() {
  const [healthData, setHealthData] = useState<HealthResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchHealth = async () => {
      try {
        const response = await fetch("http://localhost:5000/api/health");
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const data = await response.json();
        setHealthData(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to fetch health data");
      } finally {
        setLoading(false);
      }
    };

    fetchHealth();
  }, []);

  return (
    <div className="App">
      <h1>Redhead Sites Catalog</h1>
      <div className="card">
        <h2>API Health Check</h2>
        {loading && <p>Loading...</p>}
        {error && <p style={{ color: "red" }}>Error: {error}</p>}
        {healthData && (
          <div>
            <p>
              <strong>Status:</strong> {healthData.status}
            </p>
            <p>
              <strong>Message:</strong> {healthData.message}
            </p>
          </div>
        )}
      </div>
    </div>
  );
}

export default App;
