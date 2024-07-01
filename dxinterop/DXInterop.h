#pragma once
#include <d3d11.h>

class DXInterop
{
public:
	DXInterop();

	void initialize();
private:
	ID3D11Device* m_Device;
	ID3D11DeviceContext* m_Context;
};

