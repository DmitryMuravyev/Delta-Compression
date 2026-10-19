<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:msxsl="urn:schemas-microsoft-com:xslt" exclude-result-prefixes="msxsl">
  <xsl:output method="text" indent="no"/>

  <xsl:param name="FullName"/>
  <xsl:param name="BaseName"/>
  <xsl:param name="Platform"/>
  <xsl:param name="Defines"/>
  <xsl:param name="DataSize"/>

  <xsl:variable name="lowercase" select="'abcdefghijklmnopqrstuvwxyz'" />
  <xsl:variable name="uppercase" select="'ABCDEFGHIJKLMNOPQRSTUVWXYZ'" />

  <xsl:variable name="UpperBaseName" select="translate($BaseName, $lowercase, $uppercase)" />
  <xsl:variable name="LowerBaseName" select="translate($BaseName, $uppercase, $lowercase)" />

  <xsl:template match="@* | node()">
    <xsl:text>// </xsl:text><xsl:value-of select="$FullName" /><xsl:text>&#x0A;&#x0A;</xsl:text>
    <xsl:text>// Includes&#x0A;</xsl:text>
    <xsl:for-each select="//include[(@platform='all') or (@platform=$Platform)]">
      <xsl:text>#include </xsl:text><xsl:value-of select="@string" /><xsl:text>&#x0A;</xsl:text>
    </xsl:for-each>
    <xsl:text>&#x0A;</xsl:text>
    
    <xsl:text>// </xsl:text><xsl:value-of select="$UpperBaseName" /><xsl:text> definitions (you can get rid of this section if all other packaged data have the same parameters)&#x0A;</xsl:text>
    <xsl:for-each select="//define[(@platform='all') or (@platform=$Platform)]">
      <xsl:choose>
        <xsl:when test="*">
          <xsl:text>#define </xsl:text><xsl:value-of select="$UpperBaseName" />_<xsl:value-of select="@string" /><xsl:text>&#x09;&#x09;</xsl:text>
          <xsl:for-each select="*">
            <xsl:choose>
              <xsl:when test="name()='operand'">
                <xsl:value-of select="$UpperBaseName" />_<xsl:value-of select="." />
              </xsl:when>
              <xsl:when test="name()='operator'">
                <xsl:value-of select="." />
              </xsl:when>
            </xsl:choose>
          </xsl:for-each>
          <xsl:text>&#x0A;</xsl:text>
        </xsl:when>
        <xsl:otherwise>
          <xsl:text>#define </xsl:text><xsl:value-of select="$UpperBaseName" />_<xsl:value-of select="@string" /><xsl:text>&#x09;&#x09;</xsl:text><xsl:value-of select="$Defines/element[@key = current()/@shortcut]/@value" />
          <xsl:if test="@commentShortcut"><xsl:text>&#x09;</xsl:text>// <xsl:value-of select="$Defines/element[@key = current()/@commentShortcut]/@value" /></xsl:if>
          <xsl:text>&#x0A;</xsl:text>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:for-each>
    <xsl:text>&#x0A;</xsl:text>
    
    <xsl:text>// A set of constants defining the initial conditions for decompression&#x0A;</xsl:text>
    <xsl:for-each select="//constant[(@platform='all') or (@platform=$Platform)]">
      <xsl:text>static const </xsl:text><xsl:value-of select="@type" /><xsl:text>&#x20;</xsl:text><xsl:value-of select="$LowerBaseName" /><xsl:value-of select="@string" /> = {<xsl:text>&#x0A;</xsl:text><xsl:call-template name="initialValues" />
      <xsl:text>};&#x0A;</xsl:text>
    </xsl:for-each>
    <xsl:text>&#x0A;</xsl:text>

    <xsl:text>// End of </xsl:text><xsl:value-of select="$UpperBaseName" /><xsl:text> definitions&#x0A;&#x0A;</xsl:text>
    <xsl:apply-templates select="//data[(@platform=$Platform) or (@platform='all')][1]"/>
  </xsl:template>

  <xsl:template name="initialValues">
    <xsl:for-each select="*">
      <xsl:choose>
        <xsl:when test="name()='initialValue'">
          <xsl:text>&#x09;&#x09;</xsl:text><xsl:value-of select="$UpperBaseName" />_<xsl:value-of select="." /><xsl:if test="position() != last()">,<xsl:text>&#x0A;</xsl:text>
        </xsl:if>
        </xsl:when>
        <xsl:when test="name()='directive'">
          <xsl:text>#if </xsl:text><xsl:if test="not(@defined='1')">!</xsl:if>defined(<xsl:value-of select="@string" />)<xsl:text>&#x0A;</xsl:text><xsl:call-template name="initialValues" /><xsl:text>&#x0A;</xsl:text>
          <xsl:text>#endif&#x0A;</xsl:text>
        </xsl:when>
      </xsl:choose>
      <!--
      <xsl:choose>
        <xsl:when test="(current()[not(@boolShortcut)]) or ($Defines/element[@key = current()/@boolShortcut]/@value='1')">
          <xsl:text>&#x09;&#x09;</xsl:text><xsl:value-of select="$UpperBaseName" />_<xsl:value-of select="." /><xsl:if test="position() != last()">,<xsl:text>&#x0A;</xsl:text>
        </xsl:when>
      </xsl:choose>
      -->
    </xsl:for-each>
  </xsl:template>

  <xsl:template match="data">
    <xsl:text>static const </xsl:text><xsl:value-of select="@type" /><xsl:text>&#x20;</xsl:text><xsl:value-of select="$LowerBaseName" /><xsl:value-of select="@string" />[<xsl:value-of select="$DataSize" />]<xsl:if test="@progmem='1'"> PROGMEM</xsl:if><xsl:text> = </xsl:text>
  </xsl:template>
  
</xsl:stylesheet>
