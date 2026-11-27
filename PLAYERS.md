# Mecânica dos Players

Este documento define o contrato fixo dos três players de **Kurogane's Echo**. A mecânica abaixo não deve ser alterada novamente sem uma revisão explícita deste contrato.

## Recursos compartilhados

- **Poder máximo:** 3 unidades.
- **Regeneração automática:** todos os players recuperam `0,18` de poder por segundo. Essa regeneração é intencionalmente mais lenta que a regeneração de stamina (`0,85` por segundo).
- **Frutas de poder:** são coletadas automaticamente por contato com o player, sem tecla especial, e restauram `1` unidade de poder para qualquer player. Uma fruta não é coletada quando o poder já está cheio, exceto para a Kunoichi, que ainda pode consumi-la para aplicar o efeito de comida.
- **Animações especiais:** os botões são compartilhados entre os personagens; o estado executado varia conforme o player selecionado.

## Kunoichi

- **Especial primário — Eating:** requer poder disponível e representa o consumo da fruta. Ao comer, a fruta:
  1. restaura stamina, se a stamina não estiver cheia;
  2. caso a stamina esteja cheia, restaura vida;
  3. caso stamina e vida estejam cheias, concede uma vida extra, respeitando o limite de vidas.
- **Especial secundário — Cast:** é liberado depois que a Kunoichi come uma fruta e lança uma **Spine** como ataque. O Cast não consome poder.

## Peasant

- **Especial primário — Shot:** lança um **Dart** normalmente e não depende de poder nem consome poder.
- **Especial secundário — Disguise:** ativa a defesa do Peasant. Enquanto a animação estiver ativa, ataques de inimigos não causam dano. Cada uso consome `1` unidade de poder.

## Ninja Monk

- Possui somente o especial ofensivo **Blade**; não possui Cast, Disguise ou segundo especial.
- **Blade:** consome `1` unidade de poder e causa `24` de dano, acima do ataque normal dos players (`8` de dano).
- O Monk não recebe desbloqueio de especial por eliminar inimigos.

## Botões

- **Especial primário:** `F` no teclado, `Left Shoulder` no gamepad e o botão de ação primária no mobile.
- **Especial secundário:** `V` no teclado, `Right Shoulder` no gamepad e o botão de ação secundária no mobile.
- A mesma posição de botão é mantida para todos os players; apenas a animação e o efeito mudam conforme o personagem.

## Valores de implementação

Os valores numéricos deste contrato ficam centralizados em `CombatBalance.cs`: poder máximo, custo de poder, regeneração, restauração da fruta, dano do Blade e efeitos de cura da fruta da Kunoichi.
