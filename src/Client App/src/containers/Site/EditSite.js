import React from 'react';
import { Modal } from 'react-bootstrap';
import SiteForm from './SiteForm';

const EditCompany = ({ show, onClose, data }) => {

    return (
        <Modal show={show} onHide={onClose} centered size="lg">           
         <Modal.Header closeButton>
				<Modal.Title>Edit Site</Modal.Title>
			</Modal.Header>
                {<SiteForm isEdit={true} data={data} onClose={onClose}/>}
        </Modal>
    )

}

export default EditCompany;