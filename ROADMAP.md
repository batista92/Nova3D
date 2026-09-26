**uma ferramenta 3D reutilizável sobre MonoGame**, não é uma "engine”. O city builder será o primeiro cliente e, se a camada se provar boa, extraímos o núcleo para os próximos jogos.

MonoGame é adequado como fundação porque já nos entrega o framework .NET, pipeline gráfico programável, buffers, render targets, effects/shaders, input, áudio e multiplataforma. Custom effects são textuais e podem ser compilados pelo Content Pipeline ou diretamente pelo MGFXC. ([GitHub][1])

Está dividido em **duas fases grandes: primeiro provar que vale a pena; só depois construir a ferramenta.**

## Estado do projeto — 25/09/2026

```text
Fase 1 — Provas de conceito    CONCLUÍDA
Gate #1 — Terrain Triplanar    APROVADO
Gate #2 — City Benchmark       APROVADO
MonoGame                       APROVADO
Fase 2 — Nova3D M1             CONCLUÍDO
Fase 2 — Nova3D M2             CONCLUÍDO
Fase 2 — Nova3D M3             CONCLUÍDO
Fase 2 — Nova3D M4             CONCLUÍDO
Fase 3 — Física opcional       CONCLUÍDA
Fase 4 — UI opcional           CONCLUÍDA
Fase 5 — AI Developer Experience CONCLUÍDA
```

Os testes demonstraram que MonoGame fornece uma base 3D adequada quando o
renderer necessário é construído explicitamente sobre ele. As limitações
encontradas ficaram sob nosso controle: shaders, samplers, atlas, shadows,
culling, LOD e organização dos passes puderam ser diagnosticados e corrigidos
sem depender de uma camada fechada de engine.

# Fase 1 — Provas de conceito

O objetivo aqui é **tentar matar a ideia rapidamente**. Não vamos construir arquitetura bonita antes de sabermos que MonoGame consegue entregar o que precisamos.

### Teste 1 — PBR

Primeiro precisamos eliminar a principal preocupação: “3D do monogame é ruim”.

Criar uma cena extremamente pequena:

```text
Camera
DirectionalLight

Sphere
├── metal
├── plástico
└── material áspero

Plane
└── concreto
```

Implementar em HLSL:

```text
PBR
├── Albedo
├── Normal
├── Metallic
├── Roughness
├── AO
│
├── Cook-Torrance
├── GGX
├── Smith Geometry
├── Schlick Fresnel
├── Normal mapping
└── Gamma correction

```

Inicialmente nem precisamos de sistema de materiais sofisticado.

**Critério de aprovação:** conseguir algo visualmente comparável a um material PBR básico do Godot/Unity.

---

# Teste 2 — iluminação e sombras

Depois:

```text
Directional Light
       │
       ▼
 Cascaded Shadow Maps
       │
       ├── Cascade 0
       ├── Cascade 1
       ├── Cascade 2
       └── Cascade 3
```

Testar:

* PCF;
* bias;
* shadow acne;
* peter-panning;
* estabilidade das cascades;
* objetos instanciados projetando sombras.

Esse teste é importante para o city builder porque teremos câmera alta e **distâncias enormes de visualização**.

---

# Teste 3 — o assassino: Terrain Shader

Aqui repetimos exatamente o teste que matou o Stride.

```text
Terrain
│
├── Grass
│   ├── Albedo
│   ├── Normal
│   └── Roughness
│
├── Dirt
├── Rock
└── Sand

        ↓

Triplanar Mapping
+
Normal Mapping
+
Height Blend
+
Slope Blend
```

Nada de sistema de material proprietário.

Queremos literalmente:

```text
Terrain.fx
```

recebendo texturas e parâmetros.

MonoGame oficialmente suporta custom `Effect`/MGFX para esse tipo de pipeline programável. ([MonoGame Docs][2])

**Se esse teste der problema estrutural → paramos aqui.**

Esse é nosso Gate #1.

---

# Teste 4 — Terrain runtime

Se shader passar:

```text
Terrain
 ↓
Chunks 64x64
 ↓
VertexBuffer individual
 ↓
Heightmap
```

Precisamos conseguir fazer:

```text
Mouse
 ↓
Brush
 ↓
alterar heights
 ↓
atualizar chunk
 ↓
recalcular normals
 ↓
atualizar GPU
```

E somente os chunks afetados devem ser atualizados.

Testar também:

* raise/lower;
* flatten;
* smooth;
* atualização de normais;
* bounds;
* picking;
* atualização de colisão futuramente.

---

# Teste 5 — Vegetação

Agora repetimos o teste que o Stride passou.

```text
10.000 árvores
```

Mas não:

```text
10.000 DrawModel()
```

Queremos:

```text
Tree Mesh
   +
InstanceBuffer
   ↓
GPU Instancing
   ↓
10.000 árvores
```

Depois:

```text
LOD 0 → 0–50 m
LOD 1 → 50–150 m
LOD 2 → 150–400 m
Cull  → 400+ m
```

Billboard fica opcional.

---

# Teste 6 — mundo grande

Agora começa o teste realmente relacionado ao city builder.

Por exemplo:

```text
2048 × 2048 terrain

dividido em

32 × 32 chunks
```

Adicionar:

```text
Frustum Culling
Chunk LOD
Vegetation Culling
Object Culling
```

E movimentar rapidamente a câmera pelo mapa.

O objetivo não é apenas FPS.

Mediremos:

```text
FPS
CPU frame time
GPU frame time
RAM
VRAM
Draw calls
Triangles
Visible chunks
Visible instances
```

MonoGame fornece render targets e acesso suficientemente baixo ao pipeline para construirmos as passagens necessárias; não estamos presos ao `BasicEffect`. ([MonoGame Docs][3])

---

# Teste 7 — Post Processing

Só agora fazemos a cena ficar realmente bonita.

Começaria com:

```text
HDR
 ↓
Exposure
 ↓
ACES Tone Mapping
 ↓
Bloom
 ↓
FXAA
```

Depois podemos experimentar:

```text
SSAO
Fog
Color grading
TAA
```

Não colocaria tudo no primeiro renderer.

---

# Teste 8 — cena do city builder

Esse é o **Gate #2**.

Montamos uma cena fake:

```text
Grande Terrain

10.000 árvores

1.000 prédios

500 veículos fake

água

sombras

PBR

terrain triplanar

LOD

fog

post processing
```

Não existe gameplay.

É exclusivamente um benchmark.

Se tivermos:

**visual satisfatório + performance satisfatória + IA conseguindo manter os shaders**, encerramos a fase de pesquisa.

A partir daí: **MonoGame está aprovado.**

## Baseline congelada — CityBenchmark

O Teste 08 deixa de ser código descartável e passa a ser o benchmark permanente
de regressão do renderer.

```text
Build                 Release
Backend               DesktopGL
Resolução             1920 × 1080
VSync                  off
Terrain                2048 × 2048
Terrain chunks         32 × 32 (1024)
Árvores                10.000
Prédios                1.000
Veículos               500
CSM                    4 × 4096²
Água                   shader próprio
PostFX                 HDR + exposure + ACES + bloom + FXAA

Baseline observada
Frame                  3,31 ms
FPS                    302
Draw calls             748
Triângulos visíveis    61k
```

O valor de frame acima é o intervalo observado do frame completo, não uma
medição isolada de GPU. Hardware, driver e resolução devem acompanhar futuras
medições. Quando o profiler estiver disponível, a baseline será dividida em:

```text
CPU update
CPU render
GPU frame
shadow pass
terrain pass
geometry pass
water pass
post-processing
```

Toda mudança relevante no renderer deve executar novamente o CityBenchmark.
Regressões precisam ser explicadas e registradas antes de serem aceitas.

---

# Fase 2 — Nova3D

Aqui eu daria até um nome temporário, por exemplo:

```text
CityBuilder
     │
     ▼
   Nova3D
     │
     ▼
  MonoGame
```

O objetivo do `Nova3D` não seria competir com Godot.

Seria uma **camada 3D code-first extremamente amigável para IA**.

Definição de produto:

> **Nova3D — 3D rendering/world toolkit for MonoGame.**

Nova3D não substitui MonoGame e, neste estágio, não será tratada como uma
engine. MonoGame continua responsável por plataforma, game loop, input, áudio,
gráficos e tipos matemáticos fundamentais.

A filosofia seria:

> Poucas abstrações, código C#, shaders HLSL explícitos e zero magia.

## Regra arquitetural: não esconder MonoGame

Não criaremos wrappers que apenas renomeiam tipos já claros:

```text
Não criar                 Continuar usando
NovaGraphicsDevice       GraphicsDevice
NovaTexture              Texture2D
NovaVector3              Vector3
NovaMatrix               Matrix
NovaVertexBuffer         VertexBuffer
```

Nova3D abstrai os conceitos que pertencem à nossa ferramenta:

```text
Terrain
TerrainMaterial
PbrMaterial
Mesh
DirectionalLight
ShadowSystem
VegetationSystem
WorldRenderer
PostProcessPipeline
```

Isso mantém a API pequena, explícita e compreensível tanto por pessoas quanto
por IA.

## Marcos de execução

### M1 — Fundação

```text
Core
Resources
Shader management
Camera
Mesh
Material
RenderContext
RenderStatistics
```

O CityBenchmark foi movido para `Benchmarks/CityBenchmark` e consome essas APIs
sem perder comportamento ou desempenho.

### M2 — Renderer

```text
PBR
Lighting
CSM
Instancing
LOD
Culling
HDR pipeline
PostFX
Water
```

### M3 — World

```text
Terrain
Terrain chunks
Runtime deformation
Terrain materials
Vegetation
Spatial partition
World streaming
```

### M4 — Production

```text
glTF/GLB
Asset management
Hot reload de shaders
Debug rendering
Profiler
Configuration
Logging
```

Estado atual:

```text
Asset management       CONCLUÍDO (handles versionados e reload transacional)
Hot reload de shaders  CONCLUÍDO (reload transacional + materiais versionados)
Debug rendering        CONCLUÍDO
Profiler               CONCLUÍDO
Configuration          CONCLUÍDO
Logging                CONCLUÍDO
glTF/GLB                CONCLUÍDO (importador + texturas PBR + shadow pass)
```

Sistemas específicos do city builder só serão extraídos depois desses quatro
marcos. A ferramenta permanece dentro do projeto enquanto as fronteiras ainda
estiverem sendo comprovadas.

Validação final do M4: 37 arquivos GLB reais carregados e renderizados com
materiais PBR, texturas e sombras; zero falhas de importação.

---

## 1. Core

Começamos pequeno:

```text
Nova3D.Core

Game
Time
Graphics
Input
Logging
Configuration
```

MonoGame continua responsável pela plataforma.

---

## 2. Renderer

Essa será a parte principal.

```text
Nova3D.Rendering

Renderer
│
├── Camera
├── Mesh
├── Material
├── Texture
├── Shader
├── RenderTarget
│
├── Lighting
│   ├── DirectionalLight
│   ├── PointLight
│   └── SpotLight
│
├── Shadows
│   └── CascadedShadowMap
│
├── Instancing
│
└── PostProcessing
```

E quero uma API simples.

Algo conceitualmente como:

```csharp
Material material = new PbrMaterial
{
    Albedo = textures.Load("grass_albedo"),
    Normal = textures.Load("grass_normal"),
    Roughness = textures.Load("grass_roughness")
};

renderer.Draw(mesh, material, transform);
```

A IA entende isso imediatamente.

---

# 3. Shader Library

Essa parte eu considero **fundamental para o projeto**.

```text
Shaders/
│
├── Common/
│   ├── Math.fxh
│   ├── Lighting.fxh
│   ├── PBR.fxh
│   ├── Shadows.fxh
│   └── Triplanar.fxh
│
├── PBR.fx
├── Terrain.fx
├── Vegetation.fx
├── Water.fx
├── Sky.fx
└── PostProcess.fx
```

Nada escondido.

Se daqui a seis meses perguntarmos para a IA:

> “Adicione wind animation na vegetação.”

ela abre:

```text
Vegetation.fx
```

e trabalha.

Não precisa descobrir como nossa engine gera shaders.

---

# 4. Materials

Criaria um sistema pequeno:

```text
Material
   │
   ├── PbrMaterial
   ├── TerrainMaterial
   ├── VegetationMaterial
   └── UnlitMaterial
```

Não faria Shader Graph.

Não faria sistema de composição automática.

**Shader explícito > magia.**

Foi exatamente essa camada que nos causou problemas no Stride.

---

# 5. Terrain

Aqui teremos provavelmente nosso primeiro módulo realmente grande:

```text
Nova3D.Terrain

Terrain
├── TerrainChunk
├── TerrainMesh
├── HeightMap
├── TerrainMaterial
│
├── TerrainLOD
├── TerrainCulling
│
├── TerrainRaycaster
│
└── TerrainEditor
    ├── Raise
    ├── Lower
    ├── Flatten
    ├── Smooth
    └── Paint
```

E desde o início:

```text
Terrain != Renderer
```

O terrain produz dados para o renderer.

Isso vai facilitar muito manutenção.

---

# 6. Vegetation

Depois:

```text
Nova3D.Vegetation

VegetationSystem
├── InstanceBatch
├── LOD
├── Culling
├── Distribution
└── Wind
```

Árvores, pedras, arbustos, postes etc.

O city builder vai abusar disso.

---

# 7. World Rendering

Precisaremos controlar o que realmente chega à GPU.

```text
World
 │
 ├── Spatial Partition
 │
 ├── Frustum Culling
 │
 ├── Distance Culling
 │
 ├── LOD
 │
 └── Render Queue
        │
        ├── Opaque
        ├── AlphaTest
        ├── Transparent
        └── Instanced
```

Provavelmente começaria com grid/quadtree, não algo super sofisticado.

---

# 8. Asset layer

Não quero construir Blender 2.0.

Precisamos somente de:

```text
TextureLoader
ModelLoader
ShaderLoader
MaterialLoader
```

E escolher **glTF/GLB como formato 3D principal**.

Internamente podemos converter/otimizar posteriormente.

---

# 9. Debug Tools

Isso é especialmente importante porque **a IA estará desenvolvendo conosco**.

Quero poder apertar uma tecla e ver:

```text
FPS:              121
Frame:            8.2 ms

CPU:              3.1 ms
GPU:              4.7 ms

Draw calls:       284
Triangles:        1.8M
Instances:        12,482

Terrain chunks:   38/1024
Shadow draws:     91

VRAM:             ...
```

Além disso:

```text
wireframe
bounding boxes
chunk boundaries
LOD colors
shadow cascades
normals
light bounds
```

Isso torna o trabalho da IA muito mais objetivo:

> "LOD1 está aparecendo vermelho muito perto da câmera."

é muito melhor que:

> "parece que está meio lento."

---

# 10. Física

Eu **não faria física**.

Usaria biblioteca externa.

Nossa camada apenas faria integração:

```text
Nova3D.Physics
       │
       ▼
Physics Adapter
       │
       ▼
biblioteca externa
```

O mesmo vale para áudio.

Não existe motivo para escrever essas partes.

---

# Arquitetura final

Eu imagino algo assim:

```text
             CITY BUILDER
                  │
     ┌────────────┼────────────┐
     │            │            │
 Simulation      Roads      Buildings
     │
     │
     ├────────── Terrain
     │
     └────────── Vegetation
                  │
                  ▼
              ┌────────┐
              │ Nova3D │
              └────────┘
                  │
       ┌──────────┼───────────┐
       │          │           │
    Renderer    Assets      Physics
       │
       ├── PBR
       ├── HLSL
       ├── Shadows
       ├── Terrain
       ├── Instancing
       └── PostFX
                  │
                  ▼
              MonoGame
                  │
                  ▼
                 GPU
```

E existe uma decisão arquitetural que eu considero **muito importante**:

### Não separar a ferramenta do jogo imediatamente.

Começaria:

```text
CityBuilder/
├── Engine/
│   ├── Rendering/
│   ├── Terrain/
│   ├── Vegetation/
│   └── ...
│
└── Game/
    ├── Simulation/
    ├── Buildings/
    ├── Roads/
    └── ...
```

Conforme sistemas ficarem claramente genéricos:

```text
Engine/
    ↓
Nova3D/
```

Isso evita passarmos seis meses construindo uma engine imaginária para jogos que ainda não existem.

**O city builder dita as necessidades da ferramenta, não o contrário.**

---

## FASE 3 — FÍSICA OPCIONAL

```text
P1 — Fundação                                      CONCLUÍDO
   pacote Nova3D.Physics.Bepu
   fixed timestep e multithreading
   box, sphere, capsule e static box
   raycast e ownership de shapes
   benchmark headless com 1.000 corpos

P2 — Diagnóstico e filtros                         CONCLUÍDO
   collision layers e masks
   materiais físicos configuráveis
   debug draw de colliders
   estatísticas da simulação

P3 — Terrain physics                               CONCLUÍDO
   collider por terrain chunk
   streaming cria/remove collider junto do chunk
   deformação reconstrói somente chunks afetados
   teste de alinhamento visual/físico

P4 — Gameplay foundations                          CONCLUÍDO
   kinematic bodies e triggers
   sweep/shape cast
   character-controller sample
   constraints essenciais

P5 — Physics Gate                                  APROVADO
   cena visual interativa                           VALIDADA
   stress test e alocações por frame                AUTOMATIZADO
   validação de determinismo local                  AUTOMATIZADO
   documentação e template opcional                 VALIDADO
```

Regras do gate: `Nova3D` não depende de BEPU; física usa passo fixo; chunks
visuais e físicos compartilham a mesma fonte de altura; e recursos avançados
continuam acessíveis por `BepuPhysicsWorld.Simulation` sem duplicar a API BEPU.

---

## FASE 4 — UI OPCIONAL

```text
U1 — Spike Gum                                      APROVADO
   compatibilidade com MonoGame 3.8.4.1             VALIDADO
   inicialização, update e draw                     VALIDADO
   redimensionamento da janela                      VALIDADO
   mouse, teclado e gamepad                         VALIDADO
   medição de tempo e alocações                     VALIDADO

U2 — Fundação                                       CONCLUÍDO
   pacote Nova3D.UI.Gum                             IMPLEMENTADO
   lifecycle explícito                             IMPLEMENTADO
   escala virtual e DPI                            IMPLEMENTADO
   ordem de renderização                           IMPLEMENTADO
   captura de input                                IMPLEMENTADO
   ownership e dispose                             IMPLEMENTADO

U3 — Navegação                                      CONCLUÍDO
   UiScreen                                         IMPLEMENTADO
   screen stack                                     IMPLEMENTADO
   menu principal e pause menu                     IMPLEMENTADO
   modal/dialog                                     IMPLEMENTADO
   hooks de transição                               IMPLEMENTADO

U4 — HUD                                            CONCLUÍDO
   labels e barras                                  IMPLEMENTADO
   atualização sem recriar controles                IMPLEMENTADO
   ancoragem responsiva                             IMPLEMENTADO
   world-to-screen markers                          IMPLEMENTADO
   notificações                                     IMPLEMENTADO

U5 — Produção                                       CONCLUÍDO
   temas e fontes                                   VALIDADO
   navegação por gamepad                            VALIDADO
   acessibilidade básica                            VALIDADO
   template --ui                                    VALIDADO
   documentação para IA                             CONCLUÍDO
   UI benchmark e gate                              APROVADO
```

Regras: `Nova3D` não depende de Gum; a integração permanece opcional; tipos e
controles Gum não serão duplicados por wrappers Nova3D; gameplay controla o
conteúdo e as ações da UI; e o spike deve medir custo antes da extração do módulo.

---

## FASE 5 — AI DEVELOPER EXPERIENCE

Objetivo: permitir que um agente sem histórico do CityBuilder crie, valide e
publique um jogo Nova3D completo carregando apenas o contexto necessário. Mais
documentação não é sucesso por si só; o gate mede autonomia, correção e custo de
contexto.

```text
A1 — Contexto mínimo                                 CONCLUÍDO
   AI_QUICKSTART.md com caminho feliz               IMPLEMENTADO
   mapa de documentos por tarefa                    IMPLEMENTADO
   limites e ownership em formato compacto          IMPLEMENTADO
   AGENTS.md como roteador, não manual completo      IMPLEMENTADO

A2 — Receitas operacionais                           CONCLUÍDO
   formato padrão: quando/arquivos/código/validação  IMPLEMENTADO
   criar jogo e lifecycle                            IMPLEMENTADO
   GLB, material e cena                              IMPLEMENTADO
   corpo físico, esfera e câmera follow              IMPLEMENTADO
   menu, HUD, timer e game flow                      IMPLEMENTADO
   áudio, settings, saves e publicação               IMPLEMENTADO

A3 — Catálogo da API                                 CONCLUÍDO
   API_INDEX.md por tipo público                     IMPLEMENTADO
   propósito, ownership e dispose                    IMPLEMENTADO
   módulo/pacote e link para receita                 IMPLEMENTADO
   limites v0.1 próximos da API afetada              IMPLEMENTADO

A4 — Validação de um comando                         CONCLUÍDO
   eng/validate.ps1 para o toolkit                   IMPLEMENTADO
   validação de jogo gerado                          IMPLEMENTADO
   build, MGCB, testes, packages e templates         IMPLEMENTADO
   saída curta, determinística e amigável para IA    IMPLEMENTADO

A5 — Samples compiláveis                             CONCLUÍDO
   Minimal3D                                         IMPLEMENTADO
   PhysicsPlayground                                 IMPLEMENTADO
   GumMenus                                          IMPLEMENTADO
   RollingBall vertical slice                        IMPLEMENTADO
   build dos samples como regressão                  IMPLEMENTADO

A6 — Pacote para agentes                             CONCLUÍDO
   skill nova3d-game-development                     IMPLEMENTADO
   roteamento progressivo de contexto                IMPLEMENTADO
   comandos e protocolo de diagnóstico               IMPLEMENTADO
   modelo de avaliação de problemas                  IMPLEMENTADO

A7 — Presets e AI Gate                               CONCLUÍDO
   avaliar preset arcade3d com evidência dos samples IMPLEMENTADO (ADIADO)
   projeto Marble3D criado em repositório externo    IMPLEMENTADO
   catálogo real de problemas Nova3D                 IMPLEMENTADO (N3D-001)
   build executável validado                         APROVADO
   menu, áudio, física, checkpoint e vitória         VALIDADOS MANUALMENTE
   relatório de autonomia e contexto                 IMPLEMENTADO
```

### Restrições da Fase 5

- Receitas não podem esconder MonoGame nem duplicar suas APIs.
- Código específico do Marble3D permanece no repositório do jogo.
- Um preset só será criado depois que dois samples demonstrarem repetição real.
- Snippets críticos devem existir também em sample compilável ou teste.
- Documentos longos não entram no contexto inicial; o agente os abre por tópico.
- A skill roteia fontes existentes e scripts; não copia todo o manual.
- Problemas devem ser reproduzidos antes de alterar Nova3D.

### Métricas do AI Gate

```text
Contexto inicial
   somente AGENTS.md + AI_QUICKSTART.md
   AI_QUICKSTART alvo: até 250 linhas
   receita alvo: até 150 linhas por tarefa

Autonomia
   agente novo cria o projeto sem histórico do CityBuilder
   build Release e validação executados por um único comando
   nenhuma cópia de código interno da Nova3D para o jogo

Correção
   ownership e ordem de lifecycle preservados
   física e UI continuam opcionais
   problemas registrados com reprodução e evidência

Entrega
   menu, fase jogável, checkpoint, timer, áudio, settings,
   vitória/derrota e executável do Marble3D
```

O Gate #5 foi aprovado em 2026-09-25. Um agente em contexto novo entregou o
Marble3D usando os pacotes públicos/locais e a documentação distribuída pelo
template; build, publicação e startup foram validados automaticamente, e menu,
áudio, física, checkpoint e vitória foram validados manualmente. Intervenções,
contexto consumido e problemas encontrados permanecem registrados para orientar
a próxima versão da Nova3D.

Débito visual não bloqueante: a geometria verde do checkpoint aparece sobre a
parte inferior da esfera durante a sobreposição. O owner permanece `Unknown`
até existir uma reprodução mínima; não está classificado como defeito Nova3D.

## Roadmap resumido

```text
FASE 1 — VALIDAR                         CONCLUÍDA
│
├─ 01 PBR                                APROVADO
├─ 02 Shadows                            APROVADO
├─ 03 Terrain Triplanar     ← GATE #1    APROVADO
├─ 04 Terrain Runtime                    APROVADO
├─ 05 Instancing + LOD                   APROVADO
├─ 06 Large World                        APROVADO
├─ 07 Post Processing                    APROVADO
└─ 08 City Benchmark        ← GATE #2    APROVADO
             │
             ▼
       MONOGAME APROVADO
             │
             ▼
FASE 2 — NOVA3D
│
├─ M1 Fundação                           CONCLUÍDO
├─ M2 Renderer                           CONCLUÍDO
├─ M3 World                              CONCLUÍDO
└─ M4 Production                         CONCLUÍDO
             │
             ▼
          Nova3D v0.1                    CONCLUÍDA
             │
             ▼
FASE 3 — FÍSICA OPCIONAL                 APROVADA
│
└─ P1–P5 Nova3D.Physics.Bepu             CONCLUÍDO
             │
             ▼
FASE 4 — UI OPCIONAL                     APROVADA
│
└─ U1–U5 Nova3D.UI.Gum                   CONCLUÍDO
             │
             ▼
FASE 5 — AI DEVELOPER EXPERIENCE         CONCLUÍDA
│
└─ A1–A7 Marble3D              ← GATE #5 APROVADO
```

Os oito testes produziram evidência suficiente para investir na ferramenta. A
extração começa pelo M1 e deve preservar o benchmark funcionando a cada etapa.
Não criaremos ECS, scene system ou abstrações de engine sem uma necessidade
concreta do city builder.

E há um bônus interessante: o MonoGame continua suportando Windows, Linux e macOS, enquanto Vulkan/DX12 estão entrando como suporte preview na linha 3.8.5. Então podemos começar conservadoramente no backend estável e manter espaço para evoluir depois. ([GitHub][1])

[1]: https://github.com/MonoGame/MonoGame?utm_source=chatgpt.com "GitHub - MonoGame/MonoGame: One framework for creating powerful cross-platform games. · GitHub"
[2]: https://docs.monogame.net/articles/getting_started/content_pipeline/custom_effects.html?utm_source=chatgpt.com "Custom Effects | MonoGame"
[3]: https://docs.monogame.net/articles/getting_to_know/whatis/graphics/WhatIs_Render_Target.html?utm_source=chatgpt.com "What Is a Render Target? | MonoGame"
