<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.11.0" name="Act_01_Items" tilewidth="16" tileheight="16" tilecount="3" columns="3" objectalignment="bottom">
  <image source="../Tilesets/Act_01_Items.png" width="48" height="16"/>
  <grid orientation="orthogonal" width="16" height="16"/>
  <tile id="0">
    <properties>
      <property name="TileRole" value="HealthPickup"/>
      <property name="CollectibleKind" value="Health"/>
    </properties>
  </tile>
  <tile id="1">
    <properties>
      <property name="TileRole" value="StaminaPickup"/>
      <property name="CollectibleKind" value="Stamina"/>
    </properties>
  </tile>
  <tile id="2">
    <properties>
      <property name="TileRole" value="IronTalismanThird"/>
      <property name="CollectibleKind" value="IronTalismanThird"/>
      <property name="UnlockAfterBoss" type="bool" value="true"/>
    </properties>
  </tile>
</tileset>
