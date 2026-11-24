# ⚡ Nexus IT Suite

> Plataforma integrada de Gestão de Suporte de TI, Telemetria de Ativos e Portal de Chamados Multiempresa.

---

## 🎯 Visão Geral da Arquitetura

O **Nexus IT Suite** é composto por três frentes totalmente integradas:

1. **Painel de Operações & Gestão (`index.html`)**:
   - Gestão de chamados em modo Kanban e Tabela dinâmica.
   - Monitoramento e inventário de estações e servidores em tempo real.
   - Central de configurações multiempresa e setores customizáveis.
   - Relatórios operacionais, SLA e métricas de atendimento (TMA).

2. **Portal do Usuário / Solicitação de Suporte (`chamado.html`)**:
   - Abertura de chamados com interface moderna, rápida e responsiva.
   - Seleção dinâmica de empresas e setores integrados em tempo real com o painel central.
   - Preenchimento inteligente com detecção de solicitante e estação de trabalho.

3. **Nexus Remote Agent & Instalador (`agent/`)**:
   - **NexusRemoteAgent.exe**: Agente Windows em C# (.NET Framework 4.0+) para telemetria de CPU, memória, partições de disco e conectividade.
   - **Instalador-Nexus-Agent.exe**: Instalador autônomo com detecção automática de configurações, vinculação de empresa contratante e implantação facilitada.

---

## 📁 Estrutura de Diretórios

```
nexus-it-suite/
├── index.html                 # Painel do Técnico / NOC
├── chamado.html               # Portal de Chamados para Usuários
├── login.html                 # Tela de Autenticação
├── admin.html                 # Painel Administrativo
├── viewer.html                # Visualizador de Acesso Remoto
├── empresas.json              # Registro de Empresas Ativas
├── setores.json               # Registro de Departamentos
├── chamados.json              # Base de Chamados Locais
├── logo_nexus.svg             # Identidade Visual Nexus
├── agent/                     # Código e Binários do Agente Windows
│   ├── NexusRemoteAgent.cs    # Código Fonte C# do Agente de Telemetria
│   ├── NexusRemoteAgent.exe   # Executável Compilado do Agente
│   ├── InstaladorNexus.cs     # Código Fonte C# do Instalador
│   ├── Instalador-Nexus-Agent.exe # Instalador Oficial do Agente
│   ├── INSTALADOR_AGENTE.bat  # Script Auxiliar de Instalação
│   └── DESINSTALADOR_AGENTE.bat # Script de Desinstalação Limpa
└── res/                       # Ícones, Fontes e Assets Visuais
```

---

## 🚀 Como Executar Localmente

### Front-end Web:
Basta abrir o arquivo `index.html` (para o painel de suporte) ou `chamado.html` (para a central de chamados) em qualquer navegador moderno.

### Agente Windows:
Para instalar em qualquer computador Windows:
1. Execute o `Instalador-Nexus-Agent.exe` como Administrador.
2. Selecione a empresa e departamento do colaborador.
3. Clique em **Instalar e Ativar Nexus Agent**.

---

## ☁️ Próximos Passos de Nuvem & Deploy
- [ ] Conectar banco de dados na nuvem via **Supabase** (Realtime PostgreSQL).
- [ ] Hospedar os portais web na **Vercel** com domínio customizado e SSL.
- [ ] Apontar o agente C# para o endpoint de telemetria na nuvem.
