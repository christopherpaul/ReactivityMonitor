<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0"
  xmlns:wix="http://schemas.microsoft.com/wix/2006/wi">

  <xsl:output method="xml" indent="yes"/>

  <xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="wix:File[@Source='$(var.BinDir)\ReactivityMonitor.exe']/@Id">
    <xsl:attribute name="Id">ReactivityMonitor.exe</xsl:attribute>
  </xsl:template>

</xsl:stylesheet>
