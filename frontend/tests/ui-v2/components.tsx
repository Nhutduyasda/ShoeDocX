import { useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import "../../src/index.css";
import "../../src/features/universal-doc-engine/studio.css";
import { FileDrop } from "../../src/components/ui/file-drop";
function Harness() {
  const [file, setFile] = useState<File | null>(null),
    [error, setError] = useState("");
  const input = useRef<HTMLInputElement>(null);
  return (
    <main className="uds">
      <h1>Synthetic upload test</h1>
      <FileDrop
        file={file}
        disabled={false}
        inputRef={input}
        onChange={setFile}
        onError={setError}
      />
      {error && <p role="alert">{error}</p>}
      <p role="status">
        {file ? "Selected: " + file.name : "No file selected"}
      </p>
    </main>
  );
}
createRoot(document.getElementById("root")!).render(<Harness />);
