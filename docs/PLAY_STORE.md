# Publicação no Google Play

ID do app: `br.com.clubedaonca.app` (não muda depois da primeira publicação).

## 1. Chave de upload

A chave fica **fora do repositório**, em `%USERPROFILE%\.chaves\clubedaonca\`:

- `clubedaonca-upload.keystore`: a chave (alias `clubedaonca-upload`);
- `senha.txt`: o caminho, o alias e a senha, lidos pelo script.

Faça **backup dos dois arquivos** (gerenciador de senhas ou pendrive). Com a Assinatura de apps do Google Play ativada (é o padrão), quem assina o app final é o Google. Se a chave de upload se perder, dá para pedir uma nova no Play Console, mas o processo leva alguns dias.

## 2. Gerar o .aab

```powershell
# Teste interno com servidor provisório (o Release só aceita HTTPS)
.\scripts\publicar-android.ps1 -Versao 1 -ApiUrl https://<servidor-provisorio>

# Produção (padrão: https://api.clubedaonca.com.br)
.\scripts\publicar-android.ps1 -Versao 2 -VersaoExibida 1.0
```

O arquivo sai em `src/ClubeDaOnca.App/bin/Release/net10.0-android/publish/br.com.clubedaonca.app-Signed.aab`.
**`-Versao` (versionCode) precisa aumentar a cada envio.**

### Servidor provisório para o teste interno

O app Release só fala com HTTPS. Para testar sem servidor próprio, exponha a API local com um túnel:

```powershell
docker compose --profile full up -d          # API em http://localhost:5080
cloudflared tunnel --url http://localhost:5080   # mostra uma URL https://xxxx.trycloudflare.com
```

A URL do *quick tunnel* do Cloudflare muda a cada execução, e por isso exige um novo .aab. Para uma URL fixa, use um túnel nomeado do Cloudflare (com domínio próprio) ou um domínio estático gratuito do ngrok.

## 3. Play Console (conta pessoal)

1. **Criar app**: nome "Clube da Onça", idioma Português (Brasil), App, Gratuito.
2. **Painel → Configurar o app**:
   - **Política de privacidade**: URL pública de `docs/privacidade.html` (preencha antes os campos `[PREENCHER]`). Pode ser publicada pelo GitHub Pages do repositório.
   - **Acesso ao app**: informe um usuário e uma senha de teste (o app exige login).
   - **Anúncios**: não contém.
   - **Classificação de conteúdo**: questionário (categoria Utilitário/Produtividade).
   - **Público-alvo**: 18+ (quem usa o app é o titular ou a equipe do clube).
   - **Segurança dos dados**: veja a tabela abaixo.
   - **Exclusão de conta**: URL da política (item 9) ou de uma página de pedido de exclusão.
3. **Ficha da loja**: descrição curta (até 80 caracteres), descrição completa, ícone 512×512, imagem de destaque 1024×500 e pelo menos 2 capturas de tela de celular.
4. **Testar e lançar → Teste interno**: crie a lista de testadores (e-mails de contas Google), envie o .aab e publique. Os testadores instalam pelo link de participação.
5. **Teste fechado** (obrigatório para conta pessoal criada após nov/2023): pelo menos **12 testadores inscritos durante 14 dias seguidos**. Só depois disso aparece a opção de pedir acesso à **Produção**.

### Segurança dos dados (resumo para o formulário)

| Tipo de dado | Coletado | Compartilhado | Finalidade |
|---|---|---|---|
| Nome, e-mail, telefone, endereço | Sim | Sim (Asaas, provedor de e-mail, Meta/WhatsApp se autorizado) | Funcionalidade do app, gerenciamento da conta, comunicações |
| CPF/RG (outros IDs) | Sim | Sim (Asaas, para emitir boletos) | Funcionalidade do app |
| Data de nascimento, gênero, profissão | Sim | Não | Gerenciamento da conta |
| Informações financeiras (histórico de pagamentos) | Sim | Sim (Asaas) | Funcionalidade do app |
| Interações no app (reservas, eventos, entradas na portaria) | Sim | Não | Funcionalidade do app |

- Dados criptografados em trânsito: **Sim** (HTTPS).
- O usuário pode pedir a exclusão dos dados: **Sim**.
- Câmera: usada só para ler QR Code. Nada é gravado, e por isso não entra como dado coletado.
