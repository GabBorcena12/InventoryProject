Features
🛒 Product Management — Add, update, and delete products with pricing, stock, and supplier info

📦 Repackaged Items — Track smaller packages created from bulk products

💸 Sales Tracking — Record and view sales from both products and repackaged items

📑 Reports — View and export sales, inventory, low stock, and financial summaries

🧾 Operating Expenses — Log and manage daily/monthly expenses

🔁 Inventory Dashboard — Real-time UI with filtering, pagination, and search

💾 Database Backup — Trigger SQL Server .bak file backups

⚙️ CI/CD Process
1️⃣ Merge to Master Branch (Trigger)
        ↓
2️⃣ GitHub Actions Workflow Runs (CI/CD)
        ↓
3️⃣ Build Docker Image & Push to GHCR (GitHub Container Registry)
        ↓
4️⃣ Load/Pull Image into local Kind cluster (Kubernetes-in-Docker) OR GCP VM Docker (Ubuntu)
        ↓
5️⃣ kubectl apply / rollout to update the app in Kind
