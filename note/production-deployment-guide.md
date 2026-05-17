# Hướng dẫn Deploy Production - IdentityServer Signing Credential

## Tổng quan

IdentityServer cần certificate để ký JWT tokens (access tokens, identity tokens). Production **không được dùng** `AddDeveloperSigningCredential()` mà phải dùng X.509 certificate thật.

---

## Bước 1: Tạo Certificate

### Option 1: Self-signed certificate (cho testing/internal)

```bash
# Tạo certificate bằng OpenSSL
openssl req -x509 -newkey rsa:4096 \
  -keyout /tmp/identityserver-key.pem \
  -out /tmp/identityserver-cert.pem \
  -days 365 \
  -nodes \
  -subj "/CN=identityserver/O=RetailStore/C=VN"

# Chuyển sang định dạng .pfx (PKCS#12)
openssl pkcs12 -export \
  -out identityserver.pfx \
  -inkey /tmp/identityserver-key.pem \
  -in /tmp/identityserver-cert.pem \
  -passout pass:YourSecurePassword123!
```

### Option 2: Certificate từ CA đáng tin cậy (khuyến nghị production)

Mua certificate từ:
- **Let's Encrypt** (miễn phí, tự động renew)
- **DigiCert**, **GlobalSign**, **Comodo** (trả phí)

---

## Bước 2: Cấu hình Environment Variables

### Linux Server

```bash
# /etc/environment hoặc ~/.bashrc
export ASPNETCORE_ENVIRONMENT=Production
export IdentityServer__SigningCredential__KeyPath=/etc/identityserver/identityserver.pfx
export IdentityServer__SigningCredential__Password=YourSecurePassword123!

# Hoặc dùng systemd service file
```

### Docker Compose

```yaml
services:
  identityserver:
    image: retailstore/identityserver:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - IdentityServer__SigningCredential__KeyPath=/app/certs/identityserver.pfx
      - IdentityServer__SigningCredential__Password=YourSecurePassword123!
    volumes:
      - /host/path/to/certs:/app/certs:ro
    ports:
      - "5000:80"
```

### Kubernetes

```yaml
# 1. Tạo secret chứa certificate
kubectl create secret generic identityserver-cert \
  --from-file=identityserver.pfx \
  --from-literal=password=YourSecurePassword123!

# 2. Mount vào pod
apiVersion: apps/v1
kind: Deployment
metadata:
  name: identityserver
spec:
  template:
    spec:
      containers:
      - name: identityserver
        image: retailstore/identityserver:latest
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: IdentityServer__SigningCredential__KeyPath
          value: "/etc/certs/identityserver.pfx"
        - name: IdentityServer__SigningCredential__Password
          valueFrom:
            secretKeyRef:
              name: identityserver-cert
              key: password
        volumeMounts:
        - name: cert-volume
          mountPath: /etc/certs
          readOnly: true
      volumes:
      - name: cert-volume
        secret:
          secretName: identityserver-cert
```

### Azure App Service

```bash
# Upload certificate qua Azure Portal hoặc CLI
az webapp config ssl upload \
  --certificate-file ./identityserver.pfx \
  --certificate-password YourSecurePassword123! \
  --name <app-name> \
  --resource-group <resource-group>

# Set app settings
az webapp config appsettings set \
  --name <app-name> \
  --resource-group <resource-group> \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    IdentityServer__SigningCredential__KeyPath=/home/site/certs/identityserver.pfx \
    IdentityServer__SigningCredential__Password=YourSecurePassword123!
```

### AWS ECS

```bash
# Lưu certificate trong AWS Secrets Manager
aws secretsmanager create-secret \
  --name identityserver/signing-cert \
  --secret-string file://cert-secret.json

# Reference trong task definition
{
  "containerDefinitions": [
    {
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        },
        {
          "name": "IdentityServer__SigningCredential__KeyPath",
          "value": "/app/certs/identityserver.pfx"
        },
        {
          "name": "IdentityServer__SigningCredential__Password",
          "value": "secret-from-secrets-manager"
        }
      ]
    }
  ]
}
```

---

## Bước 3: Cấu hình appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "IdentityServer": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=prod-db;Database=RetailStore;Username=app;Password=xxx"
  },
  "IdentityServer": {
    "SigningCredential": {
      "KeyPath": "/etc/identityserver/identityserver.pfx",
      "Password": "${IDENTITY_SERVER_CERT_PASSWORD}"
    }
  }
}
```

---

## Bước 4: Phân quyền File Certificate (Linux)

```bash
# Tạo thư mục chứa certificate
sudo mkdir -p /etc/identityserver

# Copy certificate vào
sudo cp identityserver.pfx /etc/identityserver/

# Set quyền chỉ root đọc được
sudo chmod 600 /etc/identityserver/identityserver.pfx
sudo chown root:root /etc/identityserver/identityserver.pfx

# Nếu chạy với user khác (ví dụ: appuser)
sudo chown appuser:appuser /etc/identityserver/identityserver.pfx
```

---

## Bước 5: Deploy Application

### Build và publish

```bash
cd RetailStoreManagement/src/IdentityServer

# Build release
dotnet build -c Release

# Publish
dotnet publish -c Release -o ./publish

# Hoặc tạo Docker image
docker build -t retailstore/identityserver:latest .
```

### Deploy file (Linux systemd)

```ini
# /etc/systemd/system/identityserver.service
[Unit]
Description=Retail Store IdentityServer
After=network.target

[Service]
Type=notify
User=appuser
WorkingDirectory=/opt/identityserver
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=IdentityServer__SigningCredential__KeyPath=/etc/identityserver/identityserver.pfx
Environment=IdentityServer__SigningCredential__Password=YourSecurePassword123!
ExecStart=/usr/share/dotnet/dotnet /opt/identityserver/IdentityServer.dll
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

```bash
# Enable và start service
sudo systemctl daemon-reload
sudo systemctl enable identityserver
sudo systemctl start identityserver
sudo systemctl status identityserver
```

---

## Bước 6: Verify Deployment

### Kiểm tra logs

```bash
# Systemd
journalctl -u identityserver -f

# Docker
docker logs -f identityserver

# Kubernetes
kubectl logs -f deployment/identityserver
```

### Test endpoints

```bash
# 1. Discovery endpoint
curl https://your-domain.com/.well-known/openid-configuration

# 2. JWKS endpoint (phải có public keys)
curl https://your-domain.com/.well-known/openid-configuration/jwks

# Response mẫu:
{
  "keys": [
    {
      "kty": "RSA",
      "use": "sig",
      "kid": "xxx",
      "alg": "RS256",
      "n": "...",
      "e": "AQAB"
    }
  ]
}

# 3. Test login flow
curl -X POST https://your-domain.com/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=api&client_secret=xxx&scope=api"
```

### Kiểm tra HTTPS

```bash
# Test SSL certificate
openssl s_client -connect your-domain.com:443 -servername your-domain.com

# Hoặc dùng online tools
# https://www.ssllabs.com/ssltest/
```

---

## Bước 7: Rotate Certificate (Định kỳ)

### Khi certificate hết hạn

1. Tạo certificate mới
2. Deploy certificate mới vào server
3. Restart application
4. Update cấu hình nếu đường dẫn file thay đổi

### Tự động renew (Let's Encrypt)

```bash
# Dùng certbot
sudo certbot certonly --standalone -d your-domain.com

# Hook script để restart service sau khi renew
sudo mkdir -p /etc/letsencrypt/renewal-hooks/post/
sudo cat > /etc/letsencrypt/renewal-hooks/post/restart-identityserver.sh << 'EOF'
#!/bin/bash
systemctl restart identityserver
EOF
sudo chmod +x /etc/letsencrypt/renewal-hooks/post/restart-identityserver.sh
```

---

## Security Checklist

- [ ] Certificate lưu ở thư mục an toàn, không trong home directory
- [ ] File permission: chmod 600, chỉ user chạy app đọc được
- [ ] Password không hardcode trong source code
- [ ] Sử dụng environment variables hoặc secret manager
- [ ] Enable HTTPS only, disable HTTP
- [ ] Set HSTS header
- [ ] Certificate có thời hạn < 1 năm
- [ ] Có quy trình rotate certificate định kỳ
- [ ] Log và monitor certificate expiration
- [ ] Backup certificate an toàn (encrypted)

---

## Troubleshooting

### Lỗi: `InvalidOperationException: IdentityServer:SigningCredential:KeyPath is required in production`

**Nguyên nhân**: Thiếu environment variable hoặc config

**Giải pháp**:
```bash
# Kiểm tra biến môi trường
printenv | grep IdentityServer

# Hoặc kiểm tra file config
cat appsettings.Production.json
```

### Lỗi: `CryptographicException: The certificate file password is invalid`

**Nguyên nhân**: Password không đúng

**Giải pháp**: Kiểm tra lại password, chú ý ký tự đặc biệt cần escape

### Lỗi: `FileNotFoundException: Could not find file`

**Nguyên nhân**: Đường dẫn certificate không tồn tại

**Giải pháp**:
```bash
# Kiểm tra file có tồn tại
ls -la /etc/identityserver/identityserver.pfx

# Kiểm tra user có quyền đọc
sudo -u appuser cat /etc/identityserver/identityserver.pfx
```

### Lỗi: `CryptographicException: Permission denied`

**Nguyên nhân**: User chạy app không có quyền đọc file

**Giải pháp**:
```bash
sudo chown appuser:appuser /etc/identityserver/identityserver.pfx
sudo chmod 600 /etc/identityserver/identityserver.pfx
```

---

## Tham khảo

- [Duende IdentityServer Security Guide](https://docs.duendesoftware.com/identityserver/v7/security/)
- [ASP.NET Core Production Deployment](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/)
- [X.509 Certificate Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/security/cryptographic-services/)
